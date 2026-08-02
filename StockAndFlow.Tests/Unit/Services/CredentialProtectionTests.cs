using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Wpf.Platform;
using Xunit;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Covers the credential-protection format: the "enc1:" marker, backward compatibility with
/// legacy (unmarked) DPAPI ciphertext and plain text, and AppSettings.MigrateToEncrypted.
/// All tests that install a provider live in this class so they run sequentially and restore
/// the passthrough on dispose (SecureCredentialService.Provider is static).
/// </summary>
public class CredentialProtectionTests : IDisposable
{
    public CredentialProtectionTests()
    {
        SecureCredentialService.Provider = new WpfCredentialProtector();
    }

    public void Dispose()
    {
        SecureCredentialService.Provider = new PassthroughCredentialProtector();
    }

    private static string LegacyDpapiCiphertext(string plainText) =>
        Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser));

    [Fact]
    public void Protect_RoundTrips_AndCarriesMarker()
    {
        var protector = new WpfCredentialProtector();

        var encrypted = protector.Protect("shpat_secret_token_123");

        encrypted.Should().StartWith(SecureCredentialService.EncryptedPrefix);
        protector.IsProtected(encrypted).Should().BeTrue();
        protector.Unprotect(encrypted).Should().Be("shpat_secret_token_123");
    }

    [Fact]
    public void IsProtected_IsFalse_ForPlainText_EvenWhenItLooksLikeBase64()
    {
        var protector = new WpfCredentialProtector();

        protector.IsProtected("my-plain-password").Should().BeFalse();
        // Valid base64, > 20 chars: the old length/shape heuristic misclassified this.
        protector.IsProtected("dGVzdHN0b3JlbmFtZTEyMzQ1Ng==").Should().BeFalse();
        protector.IsProtected(null).Should().BeFalse();
        protector.IsProtected("").Should().BeFalse();
    }

    [Fact]
    public void Unprotect_ReturnsBase64LookingPlainText_AsIs_InsteadOfThrowing()
    {
        var protector = new WpfCredentialProtector();

        // Old behavior: base64 that isn't a DPAPI blob threw InvalidOperationException
        // mid-settings-load. It must now come back unchanged so migration can encrypt it.
        var base64Plain = "dGVzdHN0b3JlbmFtZTEyMzQ1Ng==";
        protector.Unprotect(base64Plain).Should().Be(base64Plain);
    }

    [Fact]
    public void Unprotect_StillDecrypts_LegacyUnmarkedDpapiValues()
    {
        var protector = new WpfCredentialProtector();

        var legacy = LegacyDpapiCiphertext("legacy-token");

        protector.IsProtected(legacy).Should().BeFalse();
        protector.Unprotect(legacy).Should().Be("legacy-token");
    }

    [Fact]
    public void Unprotect_ReturnsNull_ForUndecryptableMarkedValue()
    {
        var protector = new WpfCredentialProtector();

        // Marked as ours but not a valid DPAPI blob (e.g. copied from another machine/user):
        // must be treated as "not configured", never returned as a usable credential.
        var corrupt = SecureCredentialService.EncryptedPrefix +
                      Convert.ToBase64String(Encoding.UTF8.GetBytes("not a dpapi blob at all"));
        protector.Unprotect(corrupt).Should().BeNull();
    }

    [Fact]
    public void MigrateToEncrypted_EncryptsPlainTextCredentials()
    {
        var settings = new AppSettings
        {
            ShopifyStoreNameEncrypted = "mystore.myshopify.com",
            ShopifyApiKeyEncrypted = "plain-api-key",
            ShopifyAccessTokenEncrypted = "shpat_plain_token"
        };

        settings.MigrateToEncrypted();

        settings.ShopifyStoreNameEncrypted.Should().StartWith(SecureCredentialService.EncryptedPrefix);
        settings.ShopifyApiKeyEncrypted.Should().StartWith(SecureCredentialService.EncryptedPrefix);
        settings.ShopifyAccessTokenEncrypted.Should().StartWith(SecureCredentialService.EncryptedPrefix);
        settings.ShopifyStoreName.Should().Be("mystore.myshopify.com");
        settings.ShopifyApiKey.Should().Be("plain-api-key");
        settings.ShopifyAccessToken.Should().Be("shpat_plain_token");
    }

    [Fact]
    public void MigrateToEncrypted_IsIdempotent()
    {
        var settings = new AppSettings { ShopifyAccessToken = "shpat_token" };

        var first = settings.ShopifyAccessTokenEncrypted;
        settings.MigrateToEncrypted();

        settings.ShopifyAccessTokenEncrypted.Should().Be(first);
    }

    [Fact]
    public void MigrateToEncrypted_UpgradesLegacyCiphertext_WithoutDoubleEncrypting()
    {
        var settings = new AppSettings
        {
            ShopifyAccessTokenEncrypted = LegacyDpapiCiphertext("shpat_legacy_token")
        };

        settings.MigrateToEncrypted();

        settings.ShopifyAccessTokenEncrypted.Should().StartWith(SecureCredentialService.EncryptedPrefix);
        settings.ShopifyAccessToken.Should().Be("shpat_legacy_token");
    }

    [Fact]
    public void MigrateToEncrypted_IsNoOp_UnderPassthroughProvider()
    {
        SecureCredentialService.Provider = new PassthroughCredentialProtector();

        var settings = new AppSettings { ShopifyAccessTokenEncrypted = "shpat_plain_token" };
        settings.MigrateToEncrypted();

        settings.ShopifyAccessTokenEncrypted.Should().Be("shpat_plain_token");
    }
}
