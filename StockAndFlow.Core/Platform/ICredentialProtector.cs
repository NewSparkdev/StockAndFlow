namespace StockAndFlow.Platform
{
    /// <summary>
    /// Encrypts/decrypts sensitive strings (e.g. Shopify tokens). WPF implements with Windows DPAPI;
    /// MAUI with platform SecureStorage-backed key + AES. Wired into <see cref="StockAndFlow.Services.SecureCredentialService"/>.
    /// </summary>
    public interface ICredentialProtector
    {
        string? Protect(string? plainText);

        string? Unprotect(string? cipherText);

        bool IsProtected(string? value);
    }
}
