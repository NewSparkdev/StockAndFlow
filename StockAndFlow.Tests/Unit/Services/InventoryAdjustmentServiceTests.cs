using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Tests for InventoryAdjustmentService: recording an adjustment must change stock,
/// deleting one must reverse it exactly — including fractional weight/volume amounts.
/// </summary>
public class InventoryAdjustmentServiceTests
{
    private static (InMemoryDataService data, InventoryService inventory, InventoryAdjustmentService adjustments) CreateServices()
    {
        var data = new InMemoryDataService();
        data.InitializeAsync().Wait();
        var inventory = new InventoryService(data);
        return (data, inventory, new InventoryAdjustmentService(data, inventory));
    }

    [Fact]
    public async Task RecordAdjustmentAsync_NegativeChange_ReducesStock()
    {
        var (_, inventory, adjustments) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventory.CreateOrUpdateItemAsync(item);

        await adjustments.RecordAdjustmentAsync(
            TestDataBuilder.CreateAdjustment(item.Id, item.Name, quantityChange: -7, reason: AdjustmentReason.Damaged));

        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(93);
    }

    [Fact]
    public async Task RecordAdjustmentAsync_FractionalChange_ReducesStockExactly()
    {
        var (_, inventory, adjustments) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", quantity: 90m, unitOfMeasure: "oz");
        await inventory.CreateOrUpdateItemAsync(wax);

        await adjustments.RecordAdjustmentAsync(
            TestDataBuilder.CreateAdjustment(wax.Id, wax.Name, quantityChange: -4.25m, reason: AdjustmentReason.Lost));

        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(85.75m);
    }

    [Fact]
    public async Task DeleteAdjustmentAsync_ReversesTheStockChange()
    {
        var (_, inventory, adjustments) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventory.CreateOrUpdateItemAsync(item);

        var adjustment = await adjustments.RecordAdjustmentAsync(
            TestDataBuilder.CreateAdjustment(item.Id, item.Name, quantityChange: -10));
        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(90);

        await adjustments.DeleteAdjustmentAsync(adjustment.Id);

        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(100,
            "deleting an adjustment must put the stock back exactly where it was");
        (await adjustments.GetAllAdjustmentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAdjustmentAsync_PositiveChange_IncreasesStock()
    {
        var (_, inventory, adjustments) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem(quantity: 10);
        await inventory.CreateOrUpdateItemAsync(item);

        await adjustments.RecordAdjustmentAsync(
            TestDataBuilder.CreateAdjustment(item.Id, item.Name, quantityChange: 5, reason: AdjustmentReason.Found));

        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(15);
    }

    [Fact]
    public void FinancialImpact_UsesAbsoluteQuantity()
    {
        var adjustment = TestDataBuilder.CreateAdjustment(quantityChange: -4.5m, cost: 2m, salePrice: 6m);

        adjustment.TotalCost.Should().Be(9m);           // 4.5 × $2
        adjustment.PotentialRevenue.Should().Be(27m);   // 4.5 × $6
        adjustment.PotentialProfit.Should().Be(18m);
        adjustment.IsLoss.Should().BeTrue();
    }
}
