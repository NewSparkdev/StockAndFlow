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

        // Static HttpClient to prevent socket exhaustion
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

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
            // Validate that we're using HTTPS
            var baseUrl = $"https://{_storeName}.myshopify.com/admin/api/2024-01/";
            var uri = new Uri(baseUrl);

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Shopify API must use HTTPS");
            }

            _httpClient.BaseAddress = uri;
            _httpClient.DefaultRequestHeaders.Clear();

            // Validate access token before adding
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                throw new InvalidOperationException("Access token cannot be empty");
            }

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

            using var transaction = await _dataService.BeginTransactionAsync();
            try
            {
                SyncStatusChanged?.Invoke(this, "Fetching products from Shopify...");

                var response = await _httpClient.GetAsync("products.json");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var shopifyResponse = JsonSerializer.Deserialize<ShopifyProductsResponse>(json);

                if (shopifyResponse?.Products == null || shopifyResponse.Products.Count == 0)
                {
                    await transaction.CommitAsync();
                    return;
                }

                SyncStatusChanged?.Invoke(this, $"Syncing {shopifyResponse.Products.Count} products...");

                // Load all items once to prevent N+1 queries (PERFORMANCE FIX)
                var allItems = await _inventoryService.GetAllItemsAsync();

                foreach (var shopifyProduct in shopifyResponse.Products)
                {
                    await SyncProductToInventoryAsync(shopifyProduct, allItems);
                }

                await transaction.CommitAsync();
                SyncStatusChanged?.Invoke(this, "Sync completed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Sync failed: {ex.Message}");
            }
        }

        private async Task SyncProductToInventoryAsync(ShopifyProduct shopifyProduct, List<InventoryItem> allItems)
        {
            // Validate product data
            if (shopifyProduct == null || shopifyProduct.Id == 0 || string.IsNullOrWhiteSpace(shopifyProduct.Title))
                return;

            // Validate variants exist and have valid data
            if (shopifyProduct.Variants == null || shopifyProduct.Variants.Count == 0)
                return;

            var variant = shopifyProduct.Variants[0];
            if (variant == null)
                return;

            // Find existing item by Shopify ID (in-memory search)
            var existingItem = allItems.FirstOrDefault(i =>
                i.ShopifyProductId == shopifyProduct.Id.ToString());

            if (existingItem != null)
            {
                // Update existing item
                existingItem.Name = shopifyProduct.Title;
                existingItem.LastModifiedDate = DateTime.Now;
                existingItem.SalePrice = variant.Price;
                existingItem.QuantityOnHand = variant.InventoryQuantity;
                existingItem.Sku = variant.Sku ?? existingItem.Sku;
                existingItem.LastSyncedAt = DateTime.Now;

                await _inventoryService.CreateOrUpdateItemAsync(existingItem);
            }
            else
            {
                // Create new item
                var newItem = new InventoryItem
                {
                    Name = shopifyProduct.Title,
                    ShopifyProductId = shopifyProduct.Id.ToString(),
                    Category = shopifyProduct.ProductType ?? "Uncategorized",
                    SalePrice = variant.Price,
                    QuantityOnHand = variant.InventoryQuantity,
                    Sku = variant.Sku ?? "",
                    ShopifyVariantId = variant.Id.ToString(),
                    LastSyncedAt = DateTime.Now
                };

                await _inventoryService.CreateOrUpdateItemAsync(newItem);
            }
        }

        public async Task SyncOrdersAsync(DateTime? since = null)
        {
            if (!IsConfigured)
                return;

            using var transaction = await _dataService.BeginTransactionAsync();
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

                if (shopifyResponse?.Orders == null || shopifyResponse.Orders.Count == 0)
                {
                    await transaction.CommitAsync();
                    return;
                }

                SyncStatusChanged?.Invoke(this, $"Processing {shopifyResponse.Orders.Count} orders...");

                // Load all data once to prevent N+1 queries (PERFORMANCE FIX)
                var allInventoryItems = await _inventoryService.GetAllItemsAsync();
                var existingSales = await _salesService.GetAllSalesAsync();

                foreach (var order in shopifyResponse.Orders)
                {
                    await ProcessShopifyOrderAsync(order, allInventoryItems, existingSales);
                }

                await transaction.CommitAsync();
                SyncStatusChanged?.Invoke(this, "Order sync completed");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Order sync failed: {ex.Message}");
            }
        }

        private async Task ProcessShopifyOrderAsync(
            ShopifyOrder order,
            List<InventoryItem> allInventoryItems,
            List<Sale> existingSales)
        {
            // Validate order data
            if (order == null || order.Id == 0)
                return;

            // Check if order already processed
            if (existingSales.Any(s => s.ShopifyOrderId == order.Id.ToString()))
                return;

            var lineItems = order.LineItems ?? new List<ShopifyLineItem>();
            if (lineItems.Count == 0)
                return;

            foreach (var lineItem in lineItems)
            {
                // Validate line item data
                if (lineItem == null || lineItem.VariantId == 0 || lineItem.Quantity <= 0)
                    continue;

                // Find inventory item by Shopify product/variant ID (in-memory search)
                var inventoryItem = allInventoryItems.FirstOrDefault(i =>
                    i.ShopifyVariantId == lineItem.VariantId.ToString() ||
                    i.ShopifyProductId == lineItem.ProductId.ToString());

                if (inventoryItem != null)
                {
                    // Build customer name safely
                    var customerName = !string.IsNullOrWhiteSpace(order.Customer?.FirstName) ||
                                      !string.IsNullOrWhiteSpace(order.Customer?.LastName)
                        ? $"{order.Customer?.FirstName ?? ""} {order.Customer?.LastName ?? ""}".Trim()
                        : null;

                    await _salesService.RecordSaleAsync(
                        inventoryItem.Id,
                        lineItem.Quantity,
                        lineItem.Price,
                        customerName,
                        order.Customer?.Email,
                        $"Shopify Order #{order.OrderNumber ?? order.Id.ToString()}"
                    );
                }
            }
        }

        public async Task UpdateInventoryQuantityInShopifyAsync(InventoryItem item, long? locationId = null)
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

                // Get location if not provided (use first location)
                if (locationId == null)
                {
                    var locationsResponse = await _httpClient.GetAsync("locations.json");
                    locationsResponse.EnsureSuccessStatusCode();
                    var locationsJson = await locationsResponse.Content.ReadAsStringAsync();
                    var locations = JsonSerializer.Deserialize<ShopifyLocationsResponse>(locationsJson);
                    locationId = locations?.Locations?.FirstOrDefault()?.Id ?? 0;
                }

                if (locationId == 0)
                    return;

                // Update inventory level using the correct API endpoint
                var updatePayload = new
                {
                    location_id = locationId,
                    inventory_item_id = variant.Variant.InventoryItemId,
                    available = item.QuantityOnHand
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(updatePayload),
                    System.Text.Encoding.UTF8,
                    "application/json");

                await _httpClient.PostAsync("inventory_levels/set.json", content);

                SyncStatusChanged?.Invoke(this, $"Updated inventory for {item.Name}");
            }
            catch (Exception ex)
            {
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Failed to update {item.Name}: {ex.Message}");
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

    public class ShopifyLocationsResponse
    {
        public List<ShopifyLocation>? Locations { get; set; }
    }

    public class ShopifyLocation
    {
        public long Id { get; set; }
        public string? Name { get; set; }
    }
}
