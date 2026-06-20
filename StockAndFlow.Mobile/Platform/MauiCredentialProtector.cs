using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// MAUI implementation of <see cref="ICredentialProtector"/>. Encrypts with AES-CBC using a 256-bit
/// key stored in platform <see cref="SecureStorage"/> (iOS Keychain / Android Keystore-backed).
/// Because SecureStorage is async but the protector API is sync, the key is loaded once at startup
/// via <see cref="InitializeAsync"/>, which also wires it into <see cref="SecureCredentialService"/>.
/// </summary>
public sealed class MauiCredentialProtector : ICredentialProtector
{
    private const string KeyName = "stockandflow_cred_key_v1";
    private readonly byte[] _key;

    private MauiCredentialProtector(byte[] key) => _key = key;

    /// <summary>Loads (or creates) the AES key and installs this protector as the active provider.</summary>
    public static async Task InitializeAsync()
    {
        var existing = await SecureStorage.Default.GetAsync(KeyName);
        byte[] key;
        if (!string.IsNullOrEmpty(existing))
        {
            key = Convert.FromBase64String(existing);
        }
        else
        {
            key = RandomNumberGenerator.GetBytes(32);
            await SecureStorage.Default.SetAsync(KeyName, Convert.ToBase64String(key));
        }

        SecureCredentialService.Provider = new MauiCredentialProtector(key);
    }

    public string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            cs.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return null;

        byte[] data;
        try
        {
            data = Convert.FromBase64String(cipherText);
        }
        catch (FormatException)
        {
            // Not encrypted (legacy/plain) - return as-is for backward compatibility.
            return cipherText;
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        var iv = new byte[16];
        if (data.Length <= iv.Length)
            return cipherText;
        Array.Copy(data, iv, iv.Length);
        aes.IV = iv;

        try
        {
            using var ms = new MemoryStream(data, iv.Length, data.Length - iv.Length);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (CryptographicException)
        {
            return cipherText;
        }
    }

    public bool IsProtected(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        try
        {
            var data = Convert.FromBase64String(value);
            return data.Length > 16 && value.Length > 20;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
