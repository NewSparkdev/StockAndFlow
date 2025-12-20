namespace StockAndFlow.Models
{
    public class AppSettings
    {
        public bool AllowNegativeInventory { get; set; }
        public string DataStoragePath { get; set; } = "Data";
        public string ImageStoragePath { get; set; } = "Images";

        // Shopify settings
        public bool ShopifyEnabled { get; set; }
        public string? ShopifyStoreName { get; set; }
        public string? ShopifyApiKey { get; set; }
        public string? ShopifyAccessToken { get; set; }
        public bool AutoSyncShopify { get; set; }
        public int SyncIntervalMinutes { get; set; } = 60;
    }
}
