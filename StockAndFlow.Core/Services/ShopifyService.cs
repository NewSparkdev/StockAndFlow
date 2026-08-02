using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    /// <summary>
    /// Two-way sync with a Shopify store via the REST Admin API using a custom-app access token.
    /// Shopify returns snake_case JSON with prices as strings ("19.99"), so all DTOs carry
    /// explicit <see cref="JsonPropertyNameAttribute"/> mappings and deserialization allows
    /// numbers-from-strings — default (PascalCase, strict) parsing silently produces empty
    /// objects, which made every sync a no-op that still reported success.
    /// </summary>
    public class ShopifyService
    {
        // Shopify supports each API version for ~12 months after release. Bump this
        // periodically (and re-run ShopifyServiceTests) — requests to sunset versions
        // get silently redirected to the oldest supported version.
        private const string ApiVersion = "2026-01";

        private const int PageSize = 250; // Shopify's maximum page size

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;
        private readonly SalesService _salesService;
        private readonly HttpClient _httpClient;

        public event EventHandler<string>? SyncStatusChanged;
        public event EventHandler<Exception>? SyncError;

        /// <summary>Raised after a sync that changed local data, so views can reload.</summary>
        public event EventHandler? SyncCompleted;

        private bool _isEnabled;
        private string? _storeName;
        private string? _accessToken;
        private int _salesImported;
        private bool _adoptStockLevels;
        private int _stockLevelsAdopted;

        public bool IsConfigured => _isEnabled && !string.IsNullOrEmpty(_storeName) && !string.IsNullOrEmpty(_accessToken);

        public ShopifyService(
            IDataService dataService,
            InventoryService inventoryService,
            SalesService salesService,
            HttpMessageHandler? httpMessageHandler = null)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
            _salesService = salesService;
            // The service is a singleton, so one HttpClient for its lifetime avoids socket
            // exhaustion. The handler override exists for tests.
            _httpClient = httpMessageHandler == null
                ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) }
                : new HttpClient(httpMessageHandler) { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>Reads current credentials from settings. Safe to call again after they change.</summary>
        public async Task InitializeAsync()
        {
            var settings = await _dataService.GetSettingsAsync();
            _isEnabled = settings.ShopifyEnabled;
            _storeName = NormalizeStoreName(settings.ShopifyStoreName);
            _accessToken = settings.ShopifyAccessToken;
        }

        /// <summary>
        /// Accepts "mystore", "mystore.myshopify.com", or a pasted admin URL and reduces it
        /// to the bare store handle.
        /// </summary>
        public static string? NormalizeStoreName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            var name = raw.Trim();
            name = name.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("http://", "", StringComparison.OrdinalIgnoreCase);
            var slash = name.IndexOf('/');
            if (slash >= 0)
                name = name.Substring(0, slash);
            const string suffix = ".myshopify.com";
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - suffix.Length);
            return name;
        }

        private string BaseUrl => $"https://{_storeName}.myshopify.com/admin/api/{ApiVersion}/";

        /// <summary>GET with the access token attached per-request (the client itself stays unconfigured
        /// so credentials can change at runtime). Accepts a relative path or an absolute pagination URL.</summary>
        private Task<HttpResponseMessage> GetAsync(string pathOrUrl)
        {
            var url = pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? pathOrUrl
                : BaseUrl + pathOrUrl;
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Shopify-Access-Token", _accessToken);
            return _httpClient.SendAsync(request);
        }

        private Task<HttpResponseMessage> PostAsync(string path, object payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Shopify-Access-Token", _accessToken);
            return _httpClient.SendAsync(request);
        }

        /// <summary>
        /// Like EnsureSuccessStatusCode, but includes Shopify's error body in the message —
        /// a bare "403 Forbidden" hides whether the cause is a missing scope, protected
        /// customer data, or an expired token.
        /// </summary>
        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(); } catch { /* best effort */ }
            if (body.Length > 500) body = body.Substring(0, 500);

            Log.Error("Shopify {What} request failed: {Status} {Reason} — {Body}",
                what, (int)response.StatusCode, response.ReasonPhrase, body);

            throw new HttpRequestException(
                $"Shopify returned {(int)response.StatusCode} {response.ReasonPhrase} for {what}. {body}");
        }

        /// <summary>Extracts the rel="next" URL from Shopify's Link pagination header, if any.</summary>
        private static string? NextPageUrl(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Link", out var values))
                return null;

            foreach (var value in values)
            {
                foreach (var part in value.Split(','))
                {
                    if (!part.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var start = part.IndexOf('<');
                    var end = part.IndexOf('>');
                    if (start >= 0 && end > start)
                        return part.Substring(start + 1, end - start - 1);
                }
            }
            return null;
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!IsConfigured)
                return false;

            try
            {
                var response = await GetAsync("shop.json");
                if (!response.IsSuccessStatusCode)
                    return false;

                // Status alone isn't proof — a wrong store name can return an HTML page.
                // Make sure the body actually parses as a shop.
                var json = await response.Content.ReadAsStringAsync();
                var shop = JsonSerializer.Deserialize<ShopifyShopResponse>(json, JsonOptions);
                return shop?.Shop?.Id > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pulls products from Shopify.
        /// </summary>
        /// <param name="adoptShopifyStockLevels">
        /// Normally false: Stock &amp; Flow owns stock counts because it is the only system that
        /// sees in-person sales, so routine syncs must not touch local quantities. Pass true
        /// only for a deliberate "use Shopify's counts" action.
        /// </param>
        public async Task SyncProductsAsync(bool adoptShopifyStockLevels = false)
        {
            if (!IsConfigured)
                return;

            _adoptStockLevels = adoptShopifyStockLevels;

            using var transaction = await _dataService.BeginTransactionAsync();
            try
            {
                SyncStatusChanged?.Invoke(this, "Fetching products from Shopify...");

                var products = new List<ShopifyProduct>();
                string? url = $"products.json?limit={PageSize}";
                while (url != null)
                {
                    var response = await GetAsync(url);
                    await EnsureSuccessAsync(response, "products");

                    var json = await response.Content.ReadAsStringAsync();
                    var page = JsonSerializer.Deserialize<ShopifyProductsResponse>(json, JsonOptions);
                    Log.Information("Shopify products page returned {Count} products", page?.Products?.Count ?? 0);
                    if (page?.Products != null)
                        products.AddRange(page.Products);

                    url = NextPageUrl(response);
                }

                if (products.Count == 0)
                {
                    await transaction.CommitAsync();
                    SyncStatusChanged?.Invoke(this, "No products found in the Shopify store");
                    return;
                }

                SyncStatusChanged?.Invoke(this, $"Syncing {products.Count} products...");

                // Load all items once to prevent N+1 queries
                var allItems = await _inventoryService.GetAllItemsAsync();

                _stockLevelsAdopted = 0;
                foreach (var shopifyProduct in products)
                {
                    await SyncProductToInventoryAsync(shopifyProduct, allItems);
                }

                await transaction.CommitAsync();
                SyncStatusChanged?.Invoke(this, adoptShopifyStockLevels
                    ? $"Sync completed: {products.Count} products, {_stockLevelsAdopted} stock count(s) taken from Shopify"
                    : $"Sync completed: {products.Count} products");
                SyncCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, "Shopify product sync failed");
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Sync failed: {ex.Message}");
            }
        }

        private async Task SyncProductToInventoryAsync(ShopifyProduct shopifyProduct, List<InventoryItem> allItems)
        {
            if (shopifyProduct == null || shopifyProduct.Id == 0 || string.IsNullOrWhiteSpace(shopifyProduct.Title))
                return;

            if (shopifyProduct.Variants == null || shopifyProduct.Variants.Count == 0)
                return;

            var variant = shopifyProduct.Variants[0];
            if (variant == null)
                return;

            var existingItem = allItems.FirstOrDefault(i =>
                i.ShopifyProductId == shopifyProduct.Id.ToString());

            if (existingItem != null)
            {
                existingItem.Name = shopifyProduct.Title;
                existingItem.LastModifiedDate = DateTime.Now;
                existingItem.SalePrice = variant.Price;
                existingItem.Sku = variant.Sku ?? existingItem.Sku;
                existingItem.LastSyncedAt = DateTime.Now;

                // Stock count is NOT copied down on a routine sync. Shopify never sees
                // in-person sales, so its number is stale the moment you sell at a market —
                // overwriting from it silently resurrected stock that had already been sold,
                // and (combined with order import deducting again) made counts drift.
                // Only a deliberate "use Shopify's counts" action adopts their figure.
                if (_adoptStockLevels)
                {
                    existingItem.QuantityOnHand = variant.InventoryQuantity;
                    _stockLevelsAdopted++;
                }

                await _inventoryService.CreateOrUpdateItemAsync(existingItem);
            }
            else
            {
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

            // NO outer transaction here: RecordSaleAsync opens its own (sale + inventory
            // adjustment + BOM must be atomic together), and SQLite rejects nested
            // transactions — which made every order import throw. Per-order atomicity is
            // the right granularity anyway: a partial import is safe because ShopifyOrderId
            // dedup means the next sync resumes where this one stopped.
            try
            {
                SyncStatusChanged?.Invoke(this, "Fetching orders from Shopify...");

                var sinceParam = since.HasValue
                    ? $"&created_at_min={since.Value:yyyy-MM-ddTHH:mm:ssZ}"
                    : "";

                var orders = new List<ShopifyOrder>();
                string? url = $"orders.json?status=any&limit={PageSize}{sinceParam}";
                while (url != null)
                {
                    Log.Information("Shopify orders request: {Url}", url);
                    var response = await GetAsync(url);
                    await EnsureSuccessAsync(response, "orders");

                    var json = await response.Content.ReadAsStringAsync();
                    var page = JsonSerializer.Deserialize<ShopifyOrdersResponse>(json, JsonOptions);
                    Log.Information("Shopify orders page returned {Count} orders (payload {Length} chars)",
                        page?.Orders?.Count ?? 0, json.Length);
                    if (page?.Orders != null)
                        orders.AddRange(page.Orders);

                    url = NextPageUrl(response);
                }

                if (orders.Count == 0)
                {
                    SyncStatusChanged?.Invoke(this, "No new orders found");
                    return;
                }

                SyncStatusChanged?.Invoke(this, $"Processing {orders.Count} orders...");

                // Load all data once to prevent N+1 queries
                var allInventoryItems = await _inventoryService.GetAllItemsAsync();
                var existingSales = await _salesService.GetAllSalesAsync();

                _salesImported = 0;
                foreach (var order in orders)
                {
                    await ProcessShopifyOrderAsync(order, allInventoryItems, existingSales);
                }

                Log.Information("Shopify order sync: {Orders} orders fetched, {Sales} sales imported",
                    orders.Count, _salesImported);
                SyncStatusChanged?.Invoke(this, _salesImported > 0
                    ? $"Order sync completed: {_salesImported} new sale(s) from {orders.Count} order(s)"
                    : $"Order sync completed: {orders.Count} order(s) fetched, none new to import");
                SyncCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Shopify order sync failed");
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Order sync failed: {ex.Message}");
            }
        }

        private async Task ProcessShopifyOrderAsync(
            ShopifyOrder order,
            List<InventoryItem> allInventoryItems,
            List<Sale> existingSales)
        {
            if (order == null || order.Id == 0)
                return;

            if (existingSales.Any(s => s.ShopifyOrderId == order.Id.ToString()))
            {
                Log.Debug("Shopify order {OrderId} already imported; skipping", order.Id);
                return;
            }

            var lineItems = order.LineItems ?? new List<ShopifyLineItem>();
            if (lineItems.Count == 0)
            {
                Log.Warning("Shopify order {OrderId} has no line items", order.Id);
                return;
            }

            foreach (var lineItem in lineItems)
            {
                // variant_id/product_id are null for custom line items and deleted products
                if (lineItem == null || lineItem.VariantId is null or 0 || lineItem.Quantity <= 0)
                    continue;

                var inventoryItem = allInventoryItems.FirstOrDefault(i =>
                    i.ShopifyVariantId == lineItem.VariantId.ToString() ||
                    (lineItem.ProductId != null && i.ShopifyProductId == lineItem.ProductId.ToString()));

                if (inventoryItem != null)
                {
                    var customerName = !string.IsNullOrWhiteSpace(order.Customer?.FirstName) ||
                                      !string.IsNullOrWhiteSpace(order.Customer?.LastName)
                        ? $"{order.Customer?.FirstName ?? ""} {order.Customer?.LastName ?? ""}".Trim()
                        : null;

                    var sale = await _salesService.RecordSaleAsync(
                        inventoryItem.Id,
                        lineItem.Quantity,
                        lineItem.Price,
                        customerName,
                        order.Customer?.Email,
                        $"Shopify Order #{order.OrderNumber?.ToString() ?? order.Id.ToString()}"
                    );

                    // Stamp the Shopify order id on the sale — it is the dedup key that stops
                    // the same order being re-imported as a new sale on every sync.
                    if (sale != null)
                    {
                        sale.ShopifyOrderId = order.Id.ToString();
                        sale.ShopifyOrderNumber = order.OrderNumber?.ToString();
                        await _dataService.SaveAsync(sale);
                        existingSales.Add(sale);
                        _salesImported++;
                    }
                }
                else
                {
                    // The finished good isn't in local inventory (product sync not run, or the
                    // product was deleted in Shopify). Skipping silently used to make this look
                    // like "nothing to import".
                    Log.Warning("Shopify order {OrderId}: no local inventory item matches " +
                                "variant {VariantId} / product {ProductId} — line item skipped",
                        order.Id, lineItem.VariantId, lineItem.ProductId);
                }
            }
        }

        /// <summary>
        /// Pushes local stock counts up to Shopify for every linked item, so the storefront
        /// reflects goods already sold in person. Stock &amp; Flow is the source of truth for
        /// counts, so this is the direction stock information should flow.
        /// </summary>
        public async Task PushStockLevelsToShopifyAsync()
        {
            if (!IsConfigured)
                return;

            var items = (await _inventoryService.GetAllItemsAsync())
                .Where(i => !string.IsNullOrEmpty(i.ShopifyVariantId))
                .ToList();

            if (items.Count == 0)
                return;

            SyncStatusChanged?.Invoke(this, $"Sending stock counts to Shopify ({items.Count} items)...");

            long? locationId = null;
            int pushed = 0, failed = 0;
            foreach (var item in items)
            {
                try
                {
                    // Resolve the location once and reuse it — one lookup per item would be
                    // a needless round trip for every product.
                    locationId ??= await GetPrimaryLocationIdAsync();
                    if (locationId is null or 0)
                        break;

                    await UpdateInventoryQuantityInShopifyAsync(item, locationId, announce: false);
                    pushed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Warning(ex, "Could not push stock for {Item} to Shopify", item.Name);
                }
            }

            Log.Information("Pushed {Pushed} stock counts to Shopify ({Failed} failed)", pushed, failed);
            SyncStatusChanged?.Invoke(this, failed == 0
                ? $"Sent {pushed} stock count(s) to Shopify"
                : $"Sent {pushed} stock count(s); {failed} failed (see log)");
        }

        private async Task<long?> GetPrimaryLocationIdAsync()
        {
            var response = await GetAsync("locations.json");
            await EnsureSuccessAsync(response, "locations");
            var json = await response.Content.ReadAsStringAsync();
            var locations = JsonSerializer.Deserialize<ShopifyLocationsResponse>(json, JsonOptions);
            return locations?.Locations?.FirstOrDefault()?.Id;
        }

        /// <summary>
        /// Sends one item's local stock count to Shopify. Returns false if it couldn't be sent.
        /// </summary>
        /// <param name="announce">
        /// Per-item status/error events. The bulk push turns these off and reports once at the
        /// end, and needs failures to propagate so it can count them — hence the rethrow.
        /// </param>
        public async Task<bool> UpdateInventoryQuantityInShopifyAsync(
            InventoryItem item, long? locationId = null, bool announce = true)
        {
            if (!IsConfigured || string.IsNullOrEmpty(item.ShopifyVariantId))
                return false;

            try
            {
                // Get inventory item ID from variant
                var variantResponse = await GetAsync($"variants/{item.ShopifyVariantId}.json");
                await EnsureSuccessAsync(variantResponse, "variant");

                var variantJson = await variantResponse.Content.ReadAsStringAsync();
                var variant = JsonSerializer.Deserialize<ShopifyVariantResponse>(variantJson, JsonOptions);

                if (variant?.Variant?.InventoryItemId == null)
                {
                    Log.Warning("Shopify variant {VariantId} has no inventory_item_id; cannot set stock for {Item}",
                        item.ShopifyVariantId, item.Name);
                    return false;
                }

                locationId ??= await GetPrimaryLocationIdAsync();
                if (locationId is null or 0)
                {
                    Log.Warning("No Shopify location available; cannot set stock for {Item}", item.Name);
                    return false;
                }

                var updatePayload = new
                {
                    location_id = locationId,
                    inventory_item_id = variant.Variant.InventoryItemId,
                    // Shopify tracks whole units; fractional local stock (e.g. 90.5 oz) is
                    // rounded for the storefront only — the local count stays exact.
                    available = (int)Math.Round(item.QuantityOnHand)
                };

                var response = await PostAsync("inventory_levels/set.json", updatePayload);
                await EnsureSuccessAsync(response, "inventory level");

                if (announce)
                    SyncStatusChanged?.Invoke(this, $"Updated inventory for {item.Name}");
                return true;
            }
            catch (Exception ex)
            {
                if (!announce)
                    throw;   // bulk caller counts and logs failures itself

                Log.Error(ex, "Failed to push stock for {Item} to Shopify", item.Name);
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, $"Failed to update {item.Name}: {ex.Message}");
                return false;
            }
        }
    }

    // Shopify REST Admin API DTOs. Shopify serializes snake_case with prices as strings;
    // every property carries an explicit mapping because default resolution matches none of them.
    public class ShopifyShopResponse
    {
        [JsonPropertyName("shop")]
        public ShopifyShop? Shop { get; set; }
    }

    public class ShopifyShop
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class ShopifyProductsResponse
    {
        [JsonPropertyName("products")]
        public List<ShopifyProduct>? Products { get; set; }
    }

    public class ShopifyProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("product_type")]
        public string? ProductType { get; set; }

        [JsonPropertyName("variants")]
        public List<ShopifyVariant>? Variants { get; set; }
    }

    public class ShopifyVariant
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("sku")]
        public string? Sku { get; set; }

        [JsonPropertyName("inventory_quantity")]
        public int InventoryQuantity { get; set; }

        [JsonPropertyName("inventory_item_id")]
        public long? InventoryItemId { get; set; }
    }

    public class ShopifyVariantResponse
    {
        [JsonPropertyName("variant")]
        public ShopifyVariant? Variant { get; set; }
    }

    public class ShopifyOrdersResponse
    {
        [JsonPropertyName("orders")]
        public List<ShopifyOrder>? Orders { get; set; }
    }

    public class ShopifyOrder
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("order_number")]
        public long? OrderNumber { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("line_items")]
        public List<ShopifyLineItem>? LineItems { get; set; }

        [JsonPropertyName("customer")]
        public ShopifyCustomer? Customer { get; set; }
    }

    public class ShopifyLineItem
    {
        [JsonPropertyName("product_id")]
        public long? ProductId { get; set; }

        [JsonPropertyName("variant_id")]
        public long? VariantId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }

    public class ShopifyCustomer
    {
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    public class ShopifyLocationsResponse
    {
        [JsonPropertyName("locations")]
        public List<ShopifyLocation>? Locations { get; set; }
    }

    public class ShopifyLocation
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
