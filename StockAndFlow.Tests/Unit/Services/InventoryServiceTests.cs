using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Tests for InventoryService stock math: decimal adjustments, the negative-inventory
/// setting, the low-stock event, and soft delete.
/// </summary>
public class InventoryServiceTests
{
    private static (InMemoryDataService data, InventoryService inventory) CreateServices(bool allowNegative = false)
    {
        var data = new InMemoryDataService();
        data.InitializeAsync().Wait();
        data.SaveSettingsAsync(TestDataBuilder.CreateAppSettings(allowNegativeInventory: allowNegative)).Wait();
        return (data, new InventoryService(data));
    }

    [Fact]
    public async Task AdjustQuantityAsync_WithFractionalChange_UpdatesStockExactly()
    {
        var (_, inventory) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", quantity: 90m, unitOfMeasure: "oz");
        await inventory.CreateOrUpdateItemAsync(wax);

        var result = await inventory.AdjustQuantityAsync(wax.Id, -12.75m);

        result.Should().BeTrue();
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(77.25m);
    }

    [Fact]
    public async Task AdjustQuantityAsync_WouldGoNegative_ReturnsFalseAndLeavesStockUnchanged()
    {
        var (_, inventory) = CreateServices(allowNegative: false);
        var item = TestDataBuilder.CreateInventoryItem(quantity: 5);
        await inventory.CreateOrUpdateItemAsync(item);

        var result = await inventory.AdjustQuantityAsync(item.Id, -6);

        result.Should().BeFalse();
        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public async Task AdjustQuantityAsync_NegativeAllowedBySetting_GoesBelowZero()
    {
        var (_, inventory) = CreateServices(allowNegative: true);
        var item = TestDataBuilder.CreateInventoryItem(quantity: 5);
        await inventory.CreateOrUpdateItemAsync(item);

        var result = await inventory.AdjustQuantityAsync(item.Id, -8);

        result.Should().BeTrue();
        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(-3);
    }

    [Fact]
    public async Task AdjustQuantityAsync_DropsToMinimumLevel_RaisesLowStockDetected()
    {
        var (_, inventory) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem(quantity: 10, minimumStockLevel: 5);
        await inventory.CreateOrUpdateItemAsync(item);

        InventoryItem? alerted = null;
        inventory.LowStockDetected += (_, i) => alerted = i;

        await inventory.AdjustQuantityAsync(item.Id, -5); // 10 → 5, exactly at the alert level

        alerted.Should().NotBeNull("dropping to the alert level must fire the low-stock event");
        alerted!.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task AdjustQuantityAsync_StaysAboveMinimumLevel_DoesNotRaiseLowStock()
    {
        var (_, inventory) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem(quantity: 10, minimumStockLevel: 5);
        await inventory.CreateOrUpdateItemAsync(item);

        var raised = false;
        inventory.LowStockDetected += (_, _) => raised = true;

        await inventory.AdjustQuantityAsync(item.Id, -4); // 10 → 6, still above 5

        raised.Should().BeFalse();
    }

    [Fact]
    public async Task AdjustQuantityAsync_AlertLevelZero_NeverRaisesLowStock()
    {
        var (_, inventory) = CreateServices(allowNegative: false);
        var item = TestDataBuilder.CreateInventoryItem(quantity: 10, minimumStockLevel: 0);
        await inventory.CreateOrUpdateItemAsync(item);

        var raised = false;
        inventory.LowStockDetected += (_, _) => raised = true;

        await inventory.AdjustQuantityAsync(item.Id, -10); // 10 → 0

        raised.Should().BeFalse("an alert level of 0 means the user opted out of low-stock warnings");
    }

    [Fact]
    public async Task DeleteItemAsync_SoftDeletes_ItemMarkedDeletedNotRemoved()
    {
        var (data, inventory) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem();
        await inventory.CreateOrUpdateItemAsync(item);

        await inventory.DeleteItemAsync(item.Id);

        var stored = await data.GetByIdAsync<InventoryItem>(item.Id);
        stored.Should().NotBeNull("soft delete must keep the row for sales history");
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedDate.Should().NotBeNull();
    }

    [Fact]
    public void NewInventoryItem_DefaultsToCountedUnit()
    {
        var item = new InventoryItem();

        item.UnitOfMeasure.Should().Be("each");
        item.IsMeasured.Should().BeFalse();
    }

    [Fact]
    public void QuantityDisplay_MeasuredItem_IncludesUnit()
    {
        var wax = TestDataBuilder.CreateInventoryItem("Wax", quantity: 90.5m, unitOfMeasure: "oz");

        wax.QuantityDisplay.Should().Be($"{90.5m:0.###} oz");
        wax.IsMeasured.Should().BeTrue();
    }

    [Fact]
    public void QuantityDisplay_CountedItem_IsPlainNumber()
    {
        var mug = TestDataBuilder.CreateInventoryItem("Mug", quantity: 12);

        mug.QuantityDisplay.Should().Be("12");
    }
}
