using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// MAUI implementation of <see cref="ICredentialProtector"/>. Encrypts with AES-GCM
/// (authenticated, so tampered or wrong-key ciphertext is rejected rather than decrypting to
/// garbage) using a 256-bit key stored in platform <see cref="SecureStorage"/> (iOS Keychain /
/// Android Keystore-backed). Current-format values carry the
/// <see cref="SecureCredentialService.EncryptedPrefix"/> marker; unmarked values are legacy
/// (the old AES-CBC format or plain text) and are handled for backward compatibility.
/// Because SecureStorage is async but the protector API is sync, the key is loaded once at
/// startup via <see cref="InitializeAsync"/>, which also wires it into
/// <see cref="SecureCredentialService"/>.
/// </summary>
public sealed class MauiCredentialProtector : ICredentialProtector
{
    private const string KeyName = "stockandflow_cred_key_v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    private MauiCredentialProtector(byte[] key) => _key = key;

    /// <summary>
    /// Loads (or creates) the AES key and installs this protector as the active provider.
    /// Never leaves the non-encrypting passthrough installed: if SecureStorage is unreadable
    /// (e.g. the app was restored from a backup, so the Keystore/Keychain key that protects it
    /// is gone), the old key is unrecoverable anyway — a fresh key is generated and stored. If
    /// even storing fails, a session-only key is used: credentials saved this session won't
    /// survive a restart (they decrypt to null and the user re-enters them), but nothing is
    /// ever written to disk unencrypted.
    /// </summary>
    public static async Task InitializeAsync()
    {
        byte[]? key = null;
        try
        {
            var existing = await SecureStorage.Default.GetAsync(KeyName);
            if (!string.IsNullOrEmpty(existing))
                key = Convert.FromBase64String(existing);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Credential key could not be read from SecureStorage; generating a new key");
            try { SecureStorage.Default.Remove(KeyName); }
            catch { /* best effort — Set below will surface persistent problems */ }
        }

        if (key == null)
        {
            key = RandomNumberGenerator.GetBytes(32);
            try
            {
                await SecureStorage.Default.SetAsync(KeyName, Convert.ToBase64String(key));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Credential key could not be stored in SecureStorage; using a session-only key");
            }
        }

        SecureCredentialService.Provider = new MauiCredentialProtector(key);
    }

    public string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[NonceSize + TagSize + cipherBytes.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipherBytes.CopyTo(payload, NonceSize + TagSize);
        return SecureCredentialService.EncryptedPrefix + Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return null;

        if (cipherText.StartsWith(SecureCredentialService.EncryptedPrefix, StringComparison.Ordinal))
        {
            // Definitely ours. If authentication fails (tampered data or the key was lost and
            // regenerated), the credential is unrecoverable — return null so the app treats it
            // as "not configured" instead of using garbage.
            try
            {
                var payload = Convert.FromBase64String(
                    cipherText.Substring(SecureCredentialService.EncryptedPrefix.Length));
                if (payload.Length < NonceSize + TagSize)
                    return null;

                var cipherBytes = new byte[payload.Length - NonceSize - TagSize];
                var plainBytes = new byte[cipherBytes.Length];
                using var aes = new AesGcm(_key, TagSize);
                aes.Decrypt(
                    new ReadOnlySpan<byte>(payload, 0, NonceSize),
                    new ReadOnlySpan<byte>(payload, NonceSize + TagSize, cipherBytes.Length),
                    new ReadOnlySpan<byte>(payload, NonceSize, TagSize),
                    plainBytes);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        // Legacy value: either the old AES-CBC format (IV + ciphertext, base64) or plain text.
        byte[] data;
        try
        {
            data = Convert.FromBase64String(cipherText);
        }
        catch (FormatException)
        {
            return cipherText;
        }

        if (data.Length <= 16)
            return cipherText;

        using var cbc = Aes.Create();
        cbc.Key = _key;
        var iv = new byte[16];
        Array.Copy(data, iv, iv.Length);
        cbc.IV = iv;

        try
        {
            using var ms = new MemoryStream(data, iv.Length, data.Length - iv.Length);
            using var cs = new CryptoStream(ms, cbc.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (CryptographicException)
        {
            // Base64 but not our ciphertext — plain text that happens to look like base64.
            return cipherText;
        }
    }

    public bool IsProtected(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.StartsWith(SecureCredentialService.EncryptedPrefix, StringComparison.Ordinal);
}
