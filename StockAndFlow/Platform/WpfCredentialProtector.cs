using System;
using System.Security.Cryptography;
using System.Text;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// Windows DPAPI-based credential protection (per-user). This is the WPF implementation of
    /// <see cref="ICredentialProtector"/>; the DPAPI logic previously lived directly in
    /// SecureCredentialService and now lives here so the shared Core stays platform-agnostic.
    /// Current-format values carry the <see cref="SecureCredentialService.EncryptedPrefix"/> marker;
    /// unmarked values are legacy (older DPAPI ciphertext or plain text) and are handled for
    /// backward compatibility.
    /// </summary>
    public sealed class WpfCredentialProtector : ICredentialProtector
    {
        public string? Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);
                return SecureCredentialService.EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Failed to encrypt credential. Ensure you're running on Windows with user profile access.", ex);
            }
        }

        public string? Unprotect(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return null;

            if (cipherText.StartsWith(SecureCredentialService.EncryptedPrefix, StringComparison.Ordinal))
            {
                // Definitely ours. If it can't be decrypted (e.g. copied from another Windows
                // account), the credential is unrecoverable — return null so the app treats it
                // as "not configured" instead of crashing settings load or using garbage.
                try
                {
                    byte[] encryptedBytes = Convert.FromBase64String(
                        cipherText.Substring(SecureCredentialService.EncryptedPrefix.Length));
                    byte[] plainBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        optionalEntropy: null,
                        scope: DataProtectionScope.CurrentUser);
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

            // Legacy value: either pre-marker DPAPI ciphertext or plain text.
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FormatException)
            {
                // Not base64 — plain text, return as-is for backward compatibility.
                return cipherText;
            }
            catch (CryptographicException)
            {
                // Base64 but not a DPAPI blob (or another user's) — treat as plain text rather
                // than crashing; MigrateToEncrypted will wrap it in the current format.
                return cipherText;
            }
        }

        public bool IsProtected(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.StartsWith(SecureCredentialService.EncryptedPrefix, StringComparison.Ordinal);
    }
}
