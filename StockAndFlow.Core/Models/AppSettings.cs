using System.Text.Json.Serialization;
using StockAndFlow.Services;

namespace StockAndFlow.Models
{
    public enum StorageMode
    {
        Json,
        SQLite
    }

    public class AppSettings
    {
        public StorageMode StorageMode { get; set; } = StorageMode.SQLite;
        public bool AllowNegativeInventory { get; set; }
        public string DataStoragePath { get; set; } = "Data";
        public string ImageStoragePath { get; set; } = "Images";

        // Shopify settings
        public bool ShopifyEnabled { get; set; }

        // Encrypted credentials (stored in JSON file)
        [JsonPropertyName("ShopifyStoreName")]
        public string? ShopifyStoreNameEncrypted { get; set; }

        [JsonPropertyName("ShopifyApiKey")]
        public string? ShopifyApiKeyEncrypted { get; set; }

        [JsonPropertyName("ShopifyAccessToken")]
        public string? ShopifyAccessTokenEncrypted { get; set; }

        // Plain-text accessors (used in code, not serialized)
        [JsonIgnore]
        public string? ShopifyStoreName
        {
            get => SecureCredentialService.Decrypt(ShopifyStoreNameEncrypted);
            set => ShopifyStoreNameEncrypted = SecureCredentialService.Encrypt(value);
        }

        [JsonIgnore]
        public string? ShopifyApiKey
        {
            get => SecureCredentialService.Decrypt(ShopifyApiKeyEncrypted);
            set => ShopifyApiKeyEncrypted = SecureCredentialService.Encrypt(value);
        }

        [JsonIgnore]
        public string? ShopifyAccessToken
        {
            get => SecureCredentialService.Decrypt(ShopifyAccessTokenEncrypted);
            set => ShopifyAccessTokenEncrypted = SecureCredentialService.Encrypt(value);
        }

        public bool AutoSyncShopify { get; set; }
        public int SyncIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Migrates any existing plain-text credentials to encrypted format.
        /// Call this after loading settings from JSON to handle backward compatibility.
        /// </summary>
        public void MigrateToEncrypted()
        {
            // If the stored value doesn't look encrypted, encrypt it
            if (!string.IsNullOrEmpty(ShopifyStoreNameEncrypted) &&
                !SecureCredentialService.IsEncrypted(ShopifyStoreNameEncrypted))
            {
                var plainValue = ShopifyStoreNameEncrypted;
                ShopifyStoreNameEncrypted = SecureCredentialService.Encrypt(plainValue);
            }

            if (!string.IsNullOrEmpty(ShopifyApiKeyEncrypted) &&
                !SecureCredentialService.IsEncrypted(ShopifyApiKeyEncrypted))
            {
                var plainValue = ShopifyApiKeyEncrypted;
                ShopifyApiKeyEncrypted = SecureCredentialService.Encrypt(plainValue);
            }

            if (!string.IsNullOrEmpty(ShopifyAccessTokenEncrypted) &&
                !SecureCredentialService.IsEncrypted(ShopifyAccessTokenEncrypted))
            {
                var plainValue = ShopifyAccessTokenEncrypted;
                ShopifyAccessTokenEncrypted = SecureCredentialService.Encrypt(plainValue);
            }
        }
    }
}
