using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class ShopifyService
    {
        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;
        private readonly SalesService _salesService;
        private readonly HttpClient _httpClient;

        public event EventHandler<string>? SyncStatusChanged;
        public event EventHandler<Exception>? SyncError;

        private bool _isEnabled;
        private string? _storeName;
        private string? _accessToken;

        public bool IsConfigured => _isEnabled && !string.IsNullOrEmpty(_storeName) && !string.IsNullOrEmpty(_accessToken);

        public ShopifyService(
            IDataService dataService,
            InventoryService inventoryService,
            SalesService salesService)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
            _salesService = salesService;
            _httpClient = new HttpClient();
        }

        public async Task InitializeAsync()
        {
            var settings = await _dataService.GetSettingsAsync();
            _isEnabled = settings.ShopifyEnabled;
            _storeName = settings.ShopifyStoreName;
            _accessToken = settings.ShopifyAccessToken;

            if (IsConfigured)
            {
                ConfigureHttpClient();
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri($"https://{_storeName}.myshopify.com/admin/api/2024-01/");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", _accessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!IsConfigured)
                return false;

            try
            {
                var response = await _httpClient.GetAsync("shop.json");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task SyncProductsAsync()
        {
            if (!IsConfigured)
                return;

            try
            {
                SyncStatusChanged?.Invoke(this, "Fetching products from Shopify...");

                var response = await _httpClient.GetAsync("products.json");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var shopifyResponse = JsonSerializer.Deserialize<ShopifyProductsResponse>(json);

                if (shopifyResponse?.Products == null)
                    return;

                SyncStatusChanged?.Invoke(this, $"Syncing {shopifyResponse.Products.Count} products...");

                foreach (var shopifyProduct in shopifyResponse.Products)
                {
                    await SyncProductToInventoryAsync(shopifyProduct);
                }

                SyncStatusChanged?.Invoke(this, "Sync completed successfully");
            }
            catch (Exception ex)
            {
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Sync failed: {ex.Message}");
            }
        }

        private async Task SyncProductToInventoryAsync(ShopifyProduct shopifyProduct)
        {
            // Find existing item by Shopify ID
            var allItems = await _inventoryService.GetAllItemsAsync();
            var existingItem = allItems.FirstOrDefault(i =>
                i.ShopifyProductId == shopifyProduct.Id.ToString());

            if (existingItem != null)
            {
                // Update existing item
                existingItem.Name = shopifyProduct.Title;
                existingItem.LastModifiedDate = DateTime.Now;

                if (shopifyProduct.Variants?.Count > 0)
                {
                    var variant = shopifyProduct.Variants[0];
                    existingItem.SalePrice = variant.Price;
                    existingItem.QuantityOnHand = variant.InventoryQuantity;
                    existingItem.Sku = variant.Sku;
                }

                await _inventoryService.CreateOrUpdateItemAsync(existingItem);
            }
            else
            {
                // Create new item
                var newItem = new InventoryItem
                {
                    Name = shopifyProduct.Title,
                    ShopifyProductId = shopifyProduct.Id.ToString(),
                    Category = shopifyProduct.ProductType
                };

                if (shopifyProduct.Variants?.Count > 0)
                {
                    var variant = shopifyProduct.Variants[0];
                    newItem.SalePrice = variant.Price;
                    newItem.QuantityOnHand = variant.InventoryQuantity;
                    newItem.Sku = variant.Sku;
                    newItem.ShopifyVariantId = variant.Id.ToString();
                }

                await _inventoryService.CreateOrUpdateItemAsync(newItem);
            }
        }

        public async Task SyncOrdersAsync(DateTime? since = null)
        {
            if (!IsConfigured)
                return;

            try
            {
                SyncStatusChanged?.Invoke(this, "Fetching orders from Shopify...");

                var sinceParam = since.HasValue
                    ? $"?created_at_min={since.Value:yyyy-MM-ddTHH:mm:ssZ}"
                    : "";

                var response = await _httpClient.GetAsync($"orders.json{sinceParam}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var shopifyResponse = JsonSerializer.Deserialize<ShopifyOrdersResponse>(json);

                if (shopifyResponse?.Orders == null)
                    return;

                SyncStatusChanged?.Invoke(this, $"Processing {shopifyResponse.Orders.Count} orders...");

                foreach (var order in shopifyResponse.Orders)
                {
                    await ProcessShopifyOrderAsync(order);
                }

                SyncStatusChanged?.Invoke(this, "Order sync completed");
            }
            catch (Exception ex)
            {
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Order sync failed: {ex.Message}");
            }
        }

        private async Task ProcessShopifyOrderAsync(ShopifyOrder order)
        {
            // Check if order already processed
            var existingSales = await _salesService.GetAllSalesAsync();
            if (existingSales.Any(s => s.ShopifyOrderId == order.Id.ToString()))
                return;

            foreach (var lineItem in order.LineItems ?? new List<ShopifyLineItem>())
            {
                // Find inventory item by Shopify product/variant ID
                var allItems = await _inventoryService.GetAllItemsAsync();
                var inventoryItem = allItems.FirstOrDefault(i =>
                    i.ShopifyVariantId == lineItem.VariantId.ToString() ||
                    i.ShopifyProductId == lineItem.ProductId.ToString());

                if (inventoryItem != null)
                {
                    await _salesService.RecordSaleAsync(
                        inventoryItem.Id,
                        lineItem.Quantity,
                        lineItem.Price,
                        order.Customer?.FirstName + " " + order.Customer?.LastName,
                        order.Customer?.Email,
                        $"Shopify Order #{order.OrderNumber}"
                    );
                }
            }
        }

        public async Task UpdateInventoryQuantityInShopifyAsync(InventoryItem item)
        {
            if (!IsConfigured || string.IsNullOrEmpty(item.ShopifyVariantId))
                return;

            try
            {
                // Get inventory item ID from variant
                var variantResponse = await _httpClient.GetAsync($"variants/{item.ShopifyVariantId}.json");
                variantResponse.EnsureSuccessStatusCode();

                var variantJson = await variantResponse.Content.ReadAsStringAsync();
                var variant = JsonSerializer.Deserialize<ShopifyVariantResponse>(variantJson);

                if (variant?.Variant?.InventoryItemId == null)
                    return;

                // Update inventory level
                var updatePayload = new
                {
                    inventory_item_id = variant.Variant.InventoryItemId,
                    available = item.QuantityOnHand
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(updatePayload),
                    System.Text.Encoding.UTF8,
                    "application/json");

                await _httpClient.PostAsync("inventory_levels/set.json", content);
            }
            catch (Exception ex)
            {
                SyncError?.Invoke(this, ex);
            }
        }
    }

    // Shopify API DTOs
    public class ShopifyProductsResponse
    {
        public List<ShopifyProduct>? Products { get; set; }
    }

    public class ShopifyProduct
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ProductType { get; set; }
        public List<ShopifyVariant>? Variants { get; set; }
    }

    public class ShopifyVariant
    {
        public long Id { get; set; }
        public decimal Price { get; set; }
        public string? Sku { get; set; }
        public int InventoryQuantity { get; set; }
        public long? InventoryItemId { get; set; }
    }

    public class ShopifyVariantResponse
    {
        public ShopifyVariant? Variant { get; set; }
    }

    public class ShopifyOrdersResponse
    {
        public List<ShopifyOrder>? Orders { get; set; }
    }

    public class ShopifyOrder
    {
        public long Id { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ShopifyLineItem>? LineItems { get; set; }
        public ShopifyCustomer? Customer { get; set; }
    }

    public class ShopifyLineItem
    {
        public long ProductId { get; set; }
        public long VariantId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class ShopifyCustomer
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }
}
