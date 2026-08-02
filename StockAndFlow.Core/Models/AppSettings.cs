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
        /// Migrates stored credentials to the current encrypted format.
        /// Call this after loading settings from JSON, once the platform protector is installed.
        /// Handles both plain-text values and legacy ciphertext formats.
        /// </summary>
        public void MigrateToEncrypted()
        {
            ShopifyStoreNameEncrypted = Reencrypt(ShopifyStoreNameEncrypted);
            ShopifyApiKeyEncrypted = Reencrypt(ShopifyApiKeyEncrypted);
            ShopifyAccessTokenEncrypted = Reencrypt(ShopifyAccessTokenEncrypted);
        }

        private static string? Reencrypt(string? stored)
        {
            if (string.IsNullOrEmpty(stored) || SecureCredentialService.IsEncrypted(stored))
                return stored;

            // Decrypt first: legacy-format ciphertext yields its plain text, actual plain text
            // passes through unchanged. Encrypting the stored value directly would double-encrypt
            // legacy ciphertext. Under the passthrough provider both calls are identity, so this
            // is a no-op until the real protector is installed.
            return SecureCredentialService.Encrypt(SecureCredentialService.Decrypt(stored));
        }
    }
}
