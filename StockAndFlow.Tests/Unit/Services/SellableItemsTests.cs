using FluentAssertions;
using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Supplies-only items: a candle maker stocks wicks, jars and wax so cost-of-goods works, but
/// should never be offered those when choosing what a customer is buying.
/// </summary>
public class SellableItemsTests
{
    private readonly InMemoryDataService _data;
    private readonly InventoryService _inventory;
    private readonly BomService _bom;

    public SellableItemsTests()
    {
        _data = new InMemoryDataService();
        _data.InitializeAsync().Wait();
        _inventory = new InventoryService(_data);
        _bom = new BomService(_data);
    }

    [Fact]
    public async Task NewItems_AreSellableByDefault()
    {
        var item = await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Lavender Candle", SalePrice = 20m, QuantityOnHand = 5 });

        item.IsSellable.Should().BeTrue("nothing should vanish from a seller's list unless they say so");
        (await _inventory.GetSellableItemsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task SuppliesAreHiddenFromSellableList_ButStayInInventory()
    {
        await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Lavender Candle", SalePrice = 20m, QuantityOnHand = 5 });
        await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Candle Wick", CostPerUnit = 0.15m, QuantityOnHand = 200, IsSellable = false });
        await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Glass Jar", CostPerUnit = 1.20m, QuantityOnHand = 60, IsSellable = false });

        var sellable = await _inventory.GetSellableItemsAsync();
        sellable.Should().ContainSingle().Which.Name.Should().Be("Lavender Candle");

        var all = await _inventory.GetAllItemsAsync();
        all.Should().HaveCount(3, "supplies are still tracked — you need to know you're low on wicks");
        all.Sum(i => i.TotalValue).Should().BeGreaterThan(0, "supplies still count toward inventory value");
    }

    [Fact]
    public async Task SupplyItem_StillDeductsWhenTheProductItBuildsIsSold()
    {
        // The whole point of stocking supplies: selling the finished good consumes them.
        var wick = await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Candle Wick", CostPerUnit = 0.15m, QuantityOnHand = 100, IsSellable = false });
        var candle = await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Lavender Candle", SalePrice = 20m, QuantityOnHand = 10 });
        await _bom.SaveComponentsForItemAsync(candle.Id, new[]
        {
            new BomComponent { Id = Guid.NewGuid(), ParentItemId = candle.Id, ComponentItemId = wick.Id, QuantityPerUnit = 1m }
        });

        var sales = new SalesService(_data, _inventory, _bom);
        await sales.RecordSaleAsync(candle.Id, 3m);

        var items = await _inventory.GetAllItemsAsync();
        items.Single(i => i.Id == candle.Id).QuantityOnHand.Should().Be(7m);
        items.Single(i => i.Id == wick.Id).QuantityOnHand.Should().Be(97m,
            "a hidden supply must still be consumed by the product it builds");
    }

    [Fact]
    public async Task ComponentDetection_FindsItemsUsedInOtherProducts()
    {
        var wax = await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Soy Wax", QuantityOnHand = 500, UnitOfMeasure = "oz" });
        var candle = await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Lavender Candle", SalePrice = 20m, QuantityOnHand = 10 });
        var standalone = await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Gift Card", SalePrice = 25m, QuantityOnHand = 10 });

        await _bom.SaveComponentsForItemAsync(candle.Id, new[]
        {
            new BomComponent { Id = Guid.NewGuid(), ParentItemId = candle.Id, ComponentItemId = wax.Id, QuantityPerUnit = 8m }
        });

        var used = await _inventory.GetItemIdsUsedAsComponentsAsync();

        used.Should().Contain(wax.Id);
        used.Should().NotContain(candle.Id, "the finished good isn't a component of anything");
        used.Should().NotContain(standalone.Id);
    }

    [Fact]
    public async Task SellableFlagSurvivesAnExcelBackup()
    {
        await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Lavender Candle", SalePrice = 20m, QuantityOnHand = 5 });
        await _inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Candle Wick", CostPerUnit = 0.15m, QuantityOnHand = 200, IsSellable = false });

        var sales = new SalesService(_data, _inventory, _bom);
        var service = new ExportImportService(_data, _inventory, sales, new ExpenseService(_data));
        var path = Path.Combine(Path.GetTempPath(), $"sf-sellable-{Guid.NewGuid():N}.xlsx");

        try
        {
            await service.ExportToExcelAsync(path);
            _data.Reset();
            var result = await service.ImportFromExcelAsync(path);
            result.Success.Should().BeTrue(result.ErrorMessage);

            var restored = await _inventory.GetAllItemsAsync();
            restored.Single(i => i.Name == "Lavender Candle").IsSellable.Should().BeTrue();
            restored.Single(i => i.Name == "Candle Wick").IsSellable.Should().BeFalse(
                "restoring a backup must not put supplies back into the sales list");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
