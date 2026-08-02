using System.Net;
using System.Text;
using FluentAssertions;
using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Regression tests against REAL Shopify REST Admin API response shapes: snake_case keys and
/// prices serialized as strings. The original sync deserialized with .NET defaults (PascalCase,
/// strict types), which parsed every response into empty objects — sync reported success while
/// syncing nothing. These fixtures make that failure mode impossible to reintroduce silently.
/// </summary>
public class ShopifyServiceTests : IDisposable
{
    private readonly InMemoryDataService _dataService;
    private readonly InventoryService _inventoryService;
    private readonly SalesService _salesService;
    private readonly FakeShopifyHandler _handler;
    private readonly ShopifyService _service;

    public ShopifyServiceTests()
    {
        _dataService = new InMemoryDataService();
        _dataService.InitializeAsync().Wait();
        _inventoryService = new InventoryService(_dataService);
        _salesService = new SalesService(_dataService, _inventoryService, new BomService(_dataService));
        _handler = new FakeShopifyHandler();
        _service = new ShopifyService(_dataService, _inventoryService, _salesService, _handler);
    }

    public void Dispose() { }

    private async Task ConfigureAsync(string storeName = "teststore", string token = "shpat_test_token")
    {
        var settings = await _dataService.GetSettingsAsync();
        settings.ShopifyEnabled = true;
        settings.ShopifyStoreName = storeName;
        settings.ShopifyAccessToken = token;
        await _dataService.SaveSettingsAsync(settings);
        await _service.InitializeAsync();
    }

    // --- Real-shape fixtures (snake_case, string prices) ---

    private const string ProductsJson = """
        {
          "products": [
            {
              "id": 8001,
              "title": "Lavender Candle",
              "product_type": "Candles",
              "variants": [
                { "id": 9001, "price": "19.99", "sku": "LAV-1", "inventory_quantity": 42, "inventory_item_id": 7001 }
              ]
            },
            {
              "id": 8002,
              "title": "Vanilla Candle",
              "product_type": "Candles",
              "variants": [
                { "id": 9002, "price": "24.50", "sku": "VAN-1", "inventory_quantity": 7, "inventory_item_id": 7002 }
              ]
            }
          ]
        }
        """;

    private const string OrdersJson = """
        {
          "orders": [
            {
              "id": 5001,
              "order_number": 1001,
              "created_at": "2026-08-01T10:15:00-04:00",
              "customer": { "first_name": "Jane", "last_name": "Doe", "email": "jane@example.com" },
              "line_items": [
                { "product_id": 8001, "variant_id": 9001, "quantity": 2, "price": "19.99" },
                { "product_id": null, "variant_id": null, "quantity": 1, "price": "5.00" }
              ]
            }
          ]
        }
        """;

    private const string ShopJson = """{ "shop": { "id": 123456, "name": "Test Store" } }""";

    [Fact]
    public async Task SyncProducts_ParsesRealShopifyJson_CreatesInventoryItems()
    {
        await ConfigureAsync();
        _handler.Respond("products.json", ProductsJson);

        await _service.SyncProductsAsync();

        var items = await _inventoryService.GetAllItemsAsync();
        items.Should().HaveCount(2, "both products in the response must be parsed and created");

        var lavender = items.Single(i => i.ShopifyProductId == "8001");
        lavender.Name.Should().Be("Lavender Candle");
        lavender.SalePrice.Should().Be(19.99m, "Shopify serializes prices as strings");
        lavender.QuantityOnHand.Should().Be(42m);
        lavender.Sku.Should().Be("LAV-1");
        lavender.ShopifyVariantId.Should().Be("9001");
        lavender.Category.Should().Be("Candles");
    }

    [Fact]
    public async Task SyncProducts_SendsAccessTokenHeader_ToVersionedEndpoint()
    {
        await ConfigureAsync();
        _handler.Respond("products.json", ProductsJson);

        await _service.SyncProductsAsync();

        var request = _handler.Requests.Single();
        request.Url.Should().StartWith("https://teststore.myshopify.com/admin/api/");
        request.Url.Should().NotContain("2024-01", "the pinned API version must not be a sunset one");
        request.TokenHeader.Should().Be("shpat_test_token");
    }

    [Fact]
    public async Task SyncProducts_FollowsLinkHeaderPagination()
    {
        await ConfigureAsync();
        var page2Url = "https://teststore.myshopify.com/admin/api/2026-01/products.json?page_info=abc&limit=250";
        _handler.Respond("products.json", """
            { "products": [ { "id": 8001, "title": "Page One Candle", "variants": [ { "id": 9001, "price": "10.00", "inventory_quantity": 1 } ] } ] }
            """,
            linkHeaderNext: page2Url);
        _handler.Respond("page_info=abc", """
            { "products": [ { "id": 8002, "title": "Page Two Candle", "variants": [ { "id": 9002, "price": "12.00", "inventory_quantity": 2 } ] } ] }
            """);

        await _service.SyncProductsAsync();

        var items = await _inventoryService.GetAllItemsAsync();
        items.Should().HaveCount(2, "the sync must follow the Link rel=\"next\" header to later pages");
    }

    [Fact]
    public async Task SyncProducts_UpdatesExistingItem_MatchedByShopifyProductId()
    {
        await ConfigureAsync();
        await _inventoryService.CreateOrUpdateItemAsync(new InventoryItem
        {
            Name = "Old Name",
            ShopifyProductId = "8001",
            ShopifyVariantId = "9001",
            SalePrice = 1m,
            QuantityOnHand = 1m
        });
        _handler.Respond("products.json", ProductsJson);

        await _service.SyncProductsAsync();

        var items = await _inventoryService.GetAllItemsAsync();
        var updated = items.Single(i => i.ShopifyProductId == "8001");
        updated.Name.Should().Be("Lavender Candle");
        updated.SalePrice.Should().Be(19.99m);
        updated.QuantityOnHand.Should().Be(1m,
            "Shopify owns the catalogue (name, price, SKU) but Stock & Flow owns the stock count");
    }

    [Fact]
    public async Task SyncProducts_DoesNotOverwriteLocalStock_SoInPersonSalesSurvive()
    {
        // The bug this guards: a maker sells 5 at a market (10 -> 5), then syncs. Shopify never
        // saw those sales so it still says 10, and copying that down silently resurrected the
        // stock. Stock & Flow owns the count.
        await ConfigureAsync();
        var item = await _inventoryService.CreateOrUpdateItemAsync(new InventoryItem
        {
            Name = "Lavender Candle",
            ShopifyProductId = "8001",
            ShopifyVariantId = "9001",
            SalePrice = 19.99m,
            CostPerUnit = 8m,
            QuantityOnHand = 10m
        });

        // Five sold in person.
        await _salesService.RecordSaleAsync(item.Id, 5m);
        (await _inventoryService.GetAllItemsAsync()).Single(i => i.Id == item.Id)
            .QuantityOnHand.Should().Be(5m);

        // Shopify still reports 42 for this variant (its own stale figure).
        _handler.Respond("products.json", ProductsJson);
        await _service.SyncProductsAsync();

        var after = (await _inventoryService.GetAllItemsAsync()).Single(i => i.Id == item.Id);
        after.QuantityOnHand.Should().Be(5m, "a routine sync must not resurrect stock sold in person");
        after.SalePrice.Should().Be(19.99m, "prices and names still come down from Shopify");
        after.Name.Should().Be("Lavender Candle");
    }

    [Fact]
    public async Task SyncProducts_AdoptsShopifyStock_OnlyWhenExplicitlyAsked()
    {
        await ConfigureAsync();
        var item = await _inventoryService.CreateOrUpdateItemAsync(new InventoryItem
        {
            Name = "Lavender Candle",
            ShopifyProductId = "8001",
            ShopifyVariantId = "9001",
            SalePrice = 19.99m,
            QuantityOnHand = 3m
        });
        _handler.Respond("products.json", ProductsJson);

        await _service.SyncProductsAsync(adoptShopifyStockLevels: true);

        (await _inventoryService.GetAllItemsAsync()).Single(i => i.Id == item.Id)
            .QuantityOnHand.Should().Be(42m, "the deliberate action takes Shopify's number");
    }

    [Fact]
    public async Task SyncProducts_StillSeedsStock_ForBrandNewProducts()
    {
        // First import has no local history to protect, so Shopify's count is the opening figure.
        await ConfigureAsync();
        _handler.Respond("products.json", ProductsJson);

        await _service.SyncProductsAsync();

        var created = (await _inventoryService.GetAllItemsAsync())
            .Single(i => i.ShopifyProductId == "8001");
        created.QuantityOnHand.Should().Be(42m);
    }

    [Fact]
    public async Task OrderImport_AfterProductSync_LeavesStockMatchingShopify()
    {
        // The end-to-end sequence that used to drift: Shopify decremented on its side, product
        // sync copied that reduced number down, then order import deducted again (10 -> 8 -> 6).
        await ConfigureAsync();
        var item = await _inventoryService.CreateOrUpdateItemAsync(new InventoryItem
        {
            Name = "Lavender Candle",
            ShopifyProductId = "8001",
            ShopifyVariantId = "9001",
            SalePrice = 19.99m,
            CostPerUnit = 8m,
            QuantityOnHand = 44m       // Shopify reports 42 after selling 2
        });
        _handler.Respond("products.json", ProductsJson);
        _handler.Respond("orders.json", OrdersJson);

        await _service.SyncProductsAsync();
        await _service.SyncOrdersAsync();

        (await _inventoryService.GetAllItemsAsync()).Single(i => i.Id == item.Id)
            .QuantityOnHand.Should().Be(42m,
                "the order's 2 units come off exactly once, landing on Shopify's figure");
    }

    [Fact]
    public async Task SyncOrders_CreatesSale_AndSecondSyncDoesNotDuplicate()
    {
        await ConfigureAsync();
        await _inventoryService.CreateOrUpdateItemAsync(new InventoryItem
        {
            Name = "Lavender Candle",
            ShopifyProductId = "8001",
            ShopifyVariantId = "9001",
            SalePrice = 19.99m,
            CostPerUnit = 8m,
            QuantityOnHand = 50m
        });
        _handler.Respond("orders.json", OrdersJson);

        await _service.SyncOrdersAsync();

        var sales = await _salesService.GetAllSalesAsync();
        sales.Should().HaveCount(1, "the null-variant custom line item must be skipped");
        var sale = sales.Single();
        sale.Quantity.Should().Be(2m);
        sale.SalePricePerUnit.Should().Be(19.99m);
        sale.CustomerName.Should().Be("Jane Doe");
        sale.CustomerEmail.Should().Be("jane@example.com");
        sale.ShopifyOrderId.Should().Be("5001", "the order id is the dedup key for later syncs");

        // Same response again — the stamped ShopifyOrderId must prevent a duplicate sale.
        await _service.SyncOrdersAsync();
        (await _salesService.GetAllSalesAsync()).Should().HaveCount(1);

        // And inventory was deducted exactly once.
        var item = (await _inventoryService.GetAllItemsAsync()).Single();
        item.QuantityOnHand.Should().Be(48m);
    }

    [Fact]
    public async Task TestConnection_True_ForValidShopResponse()
    {
        await ConfigureAsync();
        _handler.Respond("shop.json", ShopJson);

        (await _service.TestConnectionAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task TestConnection_False_WhenBodyIsNotAShop()
    {
        await ConfigureAsync();
        // e.g. a wrong store name serving an HTML page with HTTP 200
        _handler.Respond("shop.json", "<html>Sorry, this shop is unavailable</html>");

        (await _service.TestConnectionAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task TestConnection_False_OnAuthFailure()
    {
        await ConfigureAsync();
        _handler.Respond("shop.json", """{ "errors": "Invalid API key or access token" }""", HttpStatusCode.Unauthorized);

        (await _service.TestConnectionAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData("mystore", "mystore")]
    [InlineData("mystore.myshopify.com", "mystore")]
    [InlineData("https://mystore.myshopify.com", "mystore")]
    [InlineData("https://mystore.myshopify.com/admin", "mystore")]
    [InlineData("  MyStore.MYSHOPIFY.com  ", "MyStore")]
    public void NormalizeStoreName_ReducesToBareHandle(string input, string expected)
    {
        ShopifyService.NormalizeStoreName(input).Should().Be(expected);
    }

    /// <summary>Serves canned responses by URL substring and records outgoing requests.</summary>
    private sealed class FakeShopifyHandler : HttpMessageHandler
    {
        private readonly List<(string Match, string Body, HttpStatusCode Status, string? NextUrl)> _routes = new();
        public List<(string Url, string? TokenHeader)> Requests { get; } = new();

        public void Respond(string urlContains, string body, HttpStatusCode status = HttpStatusCode.OK, string? linkHeaderNext = null)
            => _routes.Add((urlContains, body, status, linkHeaderNext));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            request.Headers.TryGetValues("X-Shopify-Access-Token", out var tokens);
            Requests.Add((url, tokens?.FirstOrDefault()));

            // Later-added routes win, so specific pagination routes beat the base endpoint.
            foreach (var route in Enumerable.Reverse(_routes))
            {
                if (!url.Contains(route.Match, StringComparison.OrdinalIgnoreCase))
                    continue;

                var response = new HttpResponseMessage(route.Status)
                {
                    Content = new StringContent(route.Body, Encoding.UTF8, "application/json")
                };
                if (route.NextUrl != null && !url.Contains("page_info", StringComparison.OrdinalIgnoreCase))
                    response.Headers.Add("Link", $"<{route.NextUrl}>; rel=\"next\"");
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{ "errors": "Not Found" }""")
            });
        }
    }
}
