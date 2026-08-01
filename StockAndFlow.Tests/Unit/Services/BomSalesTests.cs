using StockAndFlow.Tests.TestUtilities;
using StockAndFlow.Platform;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Tests for Bill of Materials behavior during sales: selling a finished good must
/// deduct its components (including fractional weight/volume amounts), and deleting
/// the sale must restore them exactly.
/// </summary>
public class BomSalesTests
{
    private static (InMemoryDataService data, InventoryService inventory, BomService bom, SalesService sales) CreateServices()
    {
        var data = new InMemoryDataService();
        data.InitializeAsync().Wait();
        var inventory = new InventoryService(data);
        var bom = new BomService(data);
        var sales = new SalesService(data, inventory, bom);
        return (data, inventory, bom, sales);
    }

    /// <summary>The candle scenario: a candle consumes 2.5 oz of wax and 1 wick per unit sold.</summary>
    private static async Task<(InventoryItem candle, InventoryItem wax, InventoryItem wick)> SeedCandleBomAsync(
        InventoryService inventory, BomService bom)
    {
        var candle = TestDataBuilder.CreateInventoryItem("Candle", quantity: 20);
        var wax = TestDataBuilder.CreateInventoryItem("Wax", quantity: 90m, unitOfMeasure: "oz");
        var wick = TestDataBuilder.CreateInventoryItem("Wick", quantity: 50);
        await inventory.CreateOrUpdateItemAsync(candle);
        await inventory.CreateOrUpdateItemAsync(wax);
        await inventory.CreateOrUpdateItemAsync(wick);

        await bom.SaveComponentsForItemAsync(candle.Id, new[]
        {
            new BomComponent { ParentItemId = candle.Id, ComponentItemId = wax.Id, QuantityPerUnit = 2.5m },
            new BomComponent { ParentItemId = candle.Id, ComponentItemId = wick.Id, QuantityPerUnit = 1m },
        });

        return (candle, wax, wick);
    }

    [Fact]
    public async Task RecordSaleAsync_WithBomComponents_DeductsFractionalComponentAmounts()
    {
        var (_, inventory, bom, sales) = CreateServices();
        var (candle, wax, wick) = await SeedCandleBomAsync(inventory, bom);

        await sales.RecordSaleAsync(candle.Id, quantity: 3);

        (await inventory.GetItemByIdAsync(candle.Id))!.QuantityOnHand.Should().Be(17);
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(82.5m,
            "3 candles × 2.5 oz wax must deduct exactly 7.5 oz, not a rounded amount");
        (await inventory.GetItemByIdAsync(wick.Id))!.QuantityOnHand.Should().Be(47);
    }

    [Fact]
    public async Task DeleteSaleAsync_WithRestore_RestoresBomComponentsExactly()
    {
        var (_, inventory, bom, sales) = CreateServices();
        var (candle, wax, wick) = await SeedCandleBomAsync(inventory, bom);

        var sale = await sales.RecordSaleAsync(candle.Id, quantity: 3);
        await sales.DeleteSaleAsync(sale!.Id, restoreInventory: true);

        (await inventory.GetItemByIdAsync(candle.Id))!.QuantityOnHand.Should().Be(20);
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(90m);
        (await inventory.GetItemByIdAsync(wick.Id))!.QuantityOnHand.Should().Be(50);
    }

    [Fact]
    public async Task DeleteSaleAsync_WithoutRestore_LeavesBomComponentsDeducted()
    {
        var (_, inventory, bom, sales) = CreateServices();
        var (candle, wax, _) = await SeedCandleBomAsync(inventory, bom);

        var sale = await sales.RecordSaleAsync(candle.Id, quantity: 2);
        await sales.DeleteSaleAsync(sale!.Id, restoreInventory: false);

        (await inventory.GetItemByIdAsync(candle.Id))!.QuantityOnHand.Should().Be(18);
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(85m);
    }

    [Fact]
    public async Task SaveComponentsForItemAsync_ReplacesExistingComponents()
    {
        var (_, inventory, bom, _) = CreateServices();
        var (candle, wax, wick) = await SeedCandleBomAsync(inventory, bom);

        // Re-save with only the wax component at a new amount
        await bom.SaveComponentsForItemAsync(candle.Id, new[]
        {
            new BomComponent { ParentItemId = candle.Id, ComponentItemId = wax.Id, QuantityPerUnit = 3m },
        });

        var components = await bom.GetComponentsForItemAsync(candle.Id);
        components.Should().HaveCount(1, "saving must replace the old component list, not append to it");
        components[0].ComponentItemId.Should().Be(wax.Id);
        components[0].QuantityPerUnit.Should().Be(3m);
        components.Should().NotContain(c => c.ComponentItemId == wick.Id);
    }

    [Fact]
    public async Task RecordSaleAsync_ComponentShortOnStock_GoesNegativeInsteadOfSkipping()
    {
        // The sale already happened in the real world, so component stock must follow it even
        // below zero. A negative count tells the user their numbers were off; silently skipping
        // the deduction (the old behavior) would overstate stock with no warning.
        var (_, inventory, bom, sales) = CreateServices();
        var candle = TestDataBuilder.CreateInventoryItem("Candle", quantity: 10);
        var wax = TestDataBuilder.CreateInventoryItem("Wax", quantity: 1m, unitOfMeasure: "oz");
        await inventory.CreateOrUpdateItemAsync(candle);
        await inventory.CreateOrUpdateItemAsync(wax);
        await bom.SaveComponentsForItemAsync(candle.Id, new[]
        {
            new BomComponent { ParentItemId = candle.Id, ComponentItemId = wax.Id, QuantityPerUnit = 2.5m },
        });

        var sale = await sales.RecordSaleAsync(candle.Id, quantity: 1);

        sale.Should().NotBeNull();
        (await inventory.GetItemByIdAsync(candle.Id))!.QuantityOnHand.Should().Be(9);
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(-1.5m,
            "the deduction must be recorded even when it takes the component below zero");
    }

    [Fact]
    public async Task EditSaleViewModel_ChangingQuantity_CascadesDeltaToBomComponents()
    {
        var (data, inventory, bom, sales) = CreateServices();
        var (candle, wax, wick) = await SeedCandleBomAsync(inventory, bom);

        var sale = await sales.RecordSaleAsync(candle.Id, quantity: 1);
        // After the sale: candle 19, wax 87.5, wick 49

        var transaction = new SaleTransaction
        {
            TransactionId = sale!.TransactionId,
            SaleDate = sale.SaleDate,
            Items = new List<Sale> { sale }
        };
        var vm = new EditSaleViewModel(sales, inventory, bom, transaction, Substitute.For<IDialogService>());
        vm.Items[0].Quantity = 3; // sold 2 more candles than originally recorded

        var closed = new TaskCompletionSource<bool>();
        vm.CloseRequested += (_, ok) => closed.TrySetResult(ok);
        vm.SaveCommand.Execute(null);
        var finished = await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        (finished == closed.Task && await closed.Task).Should().BeTrue("save must complete");

        (await inventory.GetItemByIdAsync(candle.Id))!.QuantityOnHand.Should().Be(17);
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(82.5m,
            "editing the sale from 1 to 3 candles must deduct 2 more candles' worth of wax (5 oz)");
        (await inventory.GetItemByIdAsync(wick.Id))!.QuantityOnHand.Should().Be(47);
    }
}
