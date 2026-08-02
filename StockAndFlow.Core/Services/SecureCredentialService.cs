using StockAndFlow.Platform;

namespace StockAndFlow.Services
{
    /// <summary>
    /// Static facade for credential encryption. The actual algorithm is supplied by the host app
    /// via <see cref="Provider"/>:
    ///   • WPF registers a Windows DPAPI provider.
    ///   • MAUI registers a SecureStorage-backed AES provider.
    /// Until a provider is set, a passthrough is used so settings load/save still works.
    /// The static API is preserved so <c>AppSettings</c> requires no changes.
    /// </summary>
    public static class SecureCredentialService
    {
        /// <summary>
        /// Marker prepended to every value encrypted by the current protectors. Lets
        /// <see cref="ICredentialProtector.IsProtected"/> answer definitively instead of
        /// guessing from base64 shape (which misclassified base64-looking plain text).
        /// Values without the marker are legacy: either an older ciphertext format or plain text.
        /// </summary>
        public const string EncryptedPrefix = "enc1:";

        /// <summary>
        /// The platform credential protector. Host apps assign this once at startup.
        /// Defaults to a non-encrypting passthrough.
        /// </summary>
        public static ICredentialProtector Provider { get; set; } = new PassthroughCredentialProtector();

        public static string? Encrypt(string? plainText) => Provider.Protect(plainText);

        public static string? Decrypt(string? encryptedText) => Provider.Unprotect(encryptedText);

        public static bool IsEncrypted(string? value) => Provider.IsProtected(value);
    }

    /// <summary>
    /// Fallback protector that performs no encryption. Used only before a real provider is set,
    /// or on platforms where secure storage is unavailable.
    /// </summary>
    public sealed class PassthroughCredentialProtector : ICredentialProtector
    {
        public string? Protect(string? plainText) => plainText;

        public string? Unprotect(string? cipherText) => cipherText;

        public bool IsProtected(string? value) => false;
    }
}
