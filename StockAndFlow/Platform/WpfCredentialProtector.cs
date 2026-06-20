using System;
using System.Security.Cryptography;
using System.Text;
using StockAndFlow.Platform;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// Windows DPAPI-based credential protection (per-user). This is the WPF implementation of
    /// <see cref="ICredentialProtector"/>; the DPAPI logic previously lived directly in
    /// SecureCredentialService and now lives here so the shared Core stays platform-agnostic.
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
                return Convert.ToBase64String(encryptedBytes);
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
                // Not encrypted or invalid base64 - return as-is for backward compatibility
                return cipherText;
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Failed to decrypt credential. The credential may have been encrypted by a different user account.", ex);
            }
        }

        public bool IsProtected(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                Convert.FromBase64String(value);
                return value.Length > 20;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
