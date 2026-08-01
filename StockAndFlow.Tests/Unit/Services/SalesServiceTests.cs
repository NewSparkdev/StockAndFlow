using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Unit tests for SalesService
/// Tests the most critical service in the application: transaction handling, inventory coordination, and revenue tracking.
/// </summary>
public class SalesServiceTests
{
    [Fact]
    public async Task RecordSaleAsync_WithValidItem_CreatesSaleAndReducesInventory()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem("Widget", quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);

        // Assert
        sale.Should().NotBeNull();
        sale!.Quantity.Should().Be(10);
        sale.ItemName.Should().Be("Widget");
        sale.SalePricePerUnit.Should().Be(20.00m); // Default sale price from TestDataBuilder
        sale.CostPerUnit.Should().Be(10.00m);

        // Verify inventory was reduced
        var updatedItem = await inventoryService.GetItemByIdAsync(item.Id);
        updatedItem.Should().NotBeNull();
        updatedItem!.QuantityOnHand.Should().Be(90);
    }

    [Fact]
    public async Task RecordSaleAsync_WithInsufficientInventory_ThrowsInvalidOperationException()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem("Widget", quantity: 5);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act & Assert
        var action = async () => await salesService.RecordSaleAsync(item.Id, quantity: 10);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient inventory*");

        // Verify inventory was not changed
        var updatedItem = await inventoryService.GetItemByIdAsync(item.Id);
        updatedItem.Should().NotBeNull();
        updatedItem!.QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public async Task RecordSaleAsync_WithNonExistentItem_ThrowsInvalidOperationException()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var action = async () => await salesService.RecordSaleAsync(nonExistentId, quantity: 1);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");

        // Verify no sale was created
        var allSales = await salesService.GetAllSalesAsync();
        allSales.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordSaleAsync_OnSuccess_RaisesSaleRecordedEvent()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var eventRaised = false;
        salesService.SaleRecorded += (sender, args) => eventRaised = true;

        // Act
        await salesService.RecordSaleAsync(item.Id, quantity: 10);

        // Assert
        eventRaised.Should().BeTrue("because SaleRecorded event should be raised");
    }

    [Fact]
    public async Task DeleteSaleAsync_WithRestoreInventory_RestoresQuantity()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem("Widget", quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Record a sale (reduces inventory to 90)
        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        sale.Should().NotBeNull();

        // Verify inventory was reduced
        var itemAfterSale = await inventoryService.GetItemByIdAsync(item.Id);
        itemAfterSale!.QuantityOnHand.Should().Be(90);

        // Act - Delete the sale and restore inventory
        await salesService.DeleteSaleAsync(sale!.Id, restoreInventory: true);

        // Assert
        // Verify sale was deleted
        var deletedSale = await salesService.GetSaleByIdAsync(sale.Id);
        deletedSale.Should().BeNull("because the sale should have been deleted");

        // Verify inventory was restored
        var itemAfterDelete = await inventoryService.GetItemByIdAsync(item.Id);
        itemAfterDelete!.QuantityOnHand.Should().Be(100, "because inventory should be restored");
    }

    [Fact]
    public async Task RecordSaleAsync_WithCustomPrice_UsesCustomPrice()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(salePrice: 20.00m, quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 1, customSalePrice: 25.00m);

        // Assert
        sale.Should().NotBeNull();
        sale!.SalePricePerUnit.Should().Be(25.00m, "because custom price should override default");
        sale.Revenue.Should().Be(25.00m); // 1 item at $25
    }

    [Fact]
    public async Task RecordSaleAsync_WithCustomerInfo_StoresCustomerData()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(
            item.Id,
            quantity: 1,
            customerName: "John Doe",
            customerEmail: "john@example.com");

        // Assert
        sale.Should().NotBeNull();
        sale!.CustomerName.Should().Be("John Doe");
        sale.CustomerEmail.Should().Be("john@example.com");
    }

    [Fact]
    public async Task RecordSaleAsync_WithTaxInfo_CalculatesTaxCorrectly()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(salePrice: 100.00m, quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(
            item.Id,
            quantity: 1,
            taxStateCode: "NY",
            taxRate: 8.875m,
            taxAmount: 8.88m);

        // Assert
        sale.Should().NotBeNull();
        sale!.TaxStateCode.Should().Be("NY");
        sale.TaxRate.Should().Be(8.875m);
        sale.TaxAmount.Should().Be(8.88m);
    }

    [Fact]
    public async Task RecordSaleAsync_WithNotes_StoresNotes()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(
            item.Id,
            quantity: 1,
            notes: "VIP customer - expedite shipping");

        // Assert
        sale.Should().NotBeNull();
        sale!.Notes.Should().Be("VIP customer - expedite shipping");
    }

    [Fact]
    public async Task RecordSaleAsync_WithAllowNegativeInventory_AllowsOverselling()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        // Enable negative inventory
        var settings = await dataService.GetSettingsAsync();
        settings.AllowNegativeInventory = true;
        await dataService.SaveSettingsAsync(settings);

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 5);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);

        // Assert
        sale.Should().NotBeNull("because negative inventory is allowed");

        // Verify inventory went negative
        var updatedItem = await inventoryService.GetItemByIdAsync(item.Id);
        updatedItem!.QuantityOnHand.Should().Be(-5);
    }

    [Fact]
    public async Task RecordSaleAsync_MultipleSalesOnSameItem_ReducesInventoryCorrectly()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act - Record multiple sales
        var sale1 = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        var sale2 = await salesService.RecordSaleAsync(item.Id, quantity: 15);
        var sale3 = await salesService.RecordSaleAsync(item.Id, quantity: 20);

        // Assert
        sale1.Should().NotBeNull();
        sale2.Should().NotBeNull();
        sale3.Should().NotBeNull();

        // Verify total reduction: 100 - 10 - 15 - 20 = 55
        var updatedItem = await inventoryService.GetItemByIdAsync(item.Id);
        updatedItem!.QuantityOnHand.Should().Be(55);
    }

    [Fact]
    public async Task DeleteSaleAsync_WithoutRestoreInventory_DoesNotRestoreQuantity()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        sale.Should().NotBeNull();

        // Act - Delete without restoring inventory
        await salesService.DeleteSaleAsync(sale!.Id, restoreInventory: false);

        // Assert
        // Verify sale was deleted
        var deletedSale = await salesService.GetSaleByIdAsync(sale.Id);
        deletedSale.Should().BeNull();

        // Verify inventory was NOT restored (still at 90)
        var itemAfterDelete = await inventoryService.GetItemByIdAsync(item.Id);
        itemAfterDelete!.QuantityOnHand.Should().Be(90, "because inventory should not be restored");
    }

    [Fact]
    public async Task DeleteSaleAsync_RaisesSaleRecordedEvent()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        sale.Should().NotBeNull();

        var eventRaised = false;
        salesService.SaleRecorded += (sender, args) => eventRaised = true;

        // Act
        await salesService.DeleteSaleAsync(sale!.Id, restoreInventory: true);

        // Assert
        eventRaised.Should().BeTrue("because SaleRecorded event should be raised on delete");
    }

    [Fact]
    public async Task UpdateSaleAsync_UpdatesExistingSale()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        sale.Should().NotBeNull();

        // Modify the sale
        sale!.Notes = "Updated notes";
        sale.CustomerName = "Updated Customer";

        // Act
        var updatedSale = await salesService.UpdateSaleAsync(sale);

        // Assert
        updatedSale.Notes.Should().Be("Updated notes");
        updatedSale.CustomerName.Should().Be("Updated Customer");

        // Verify it was persisted
        var retrievedSale = await salesService.GetSaleByIdAsync(sale.Id);
        retrievedSale!.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task UpdateSaleAsync_RaisesSaleRecordedEvent()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        sale.Should().NotBeNull();

        var eventRaised = false;
        salesService.SaleRecorded += (sender, args) => eventRaised = true;

        // Act
        await salesService.UpdateSaleAsync(sale!);

        // Assert
        eventRaised.Should().BeTrue("because SaleRecorded event should be raised on update");
    }

    [Fact]
    public async Task GetSalesByItemAsync_ReturnsOnlySalesForSpecificItem()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item1 = TestDataBuilder.CreateInventoryItem("Widget", quantity: 100);
        var item2 = TestDataBuilder.CreateInventoryItem("Gadget", quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item1);
        await inventoryService.CreateOrUpdateItemAsync(item2);

        // Record sales for both items
        await salesService.RecordSaleAsync(item1.Id, quantity: 5);
        await salesService.RecordSaleAsync(item1.Id, quantity: 10);
        await salesService.RecordSaleAsync(item2.Id, quantity: 3);

        // Act
        var item1Sales = await salesService.GetSalesByItemAsync(item1.Id);

        // Assert
        item1Sales.Should().HaveCount(2, "because item1 has 2 sales");
        item1Sales.Should().OnlyContain(s => s.InventoryItemId == item1.Id);
    }

    [Fact]
    public async Task GetSalesByDateRangeAsync_ReturnsOnlySalesInRange()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Record sales with different dates
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);

        await salesService.RecordSaleAsync(item.Id, quantity: 5, saleDate: new DateTime(2024, 1, 15)); // In range
        await salesService.RecordSaleAsync(item.Id, quantity: 10, saleDate: new DateTime(2024, 1, 20)); // In range
        await salesService.RecordSaleAsync(item.Id, quantity: 3, saleDate: new DateTime(2023, 12, 31)); // Out of range

        // Act
        var salesInRange = await salesService.GetSalesByDateRangeAsync(startDate, endDate);

        // Assert
        salesInRange.Should().HaveCount(2, "because only 2 sales are in the date range");
        salesInRange.Should().OnlyContain(s => s.SaleDate >= startDate && s.SaleDate <= endDate);
    }

    [Fact]
    public async Task GetAllSalesAsync_ReturnsAllSales()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item1 = TestDataBuilder.CreateInventoryItem(quantity: 100);
        var item2 = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item1);
        await inventoryService.CreateOrUpdateItemAsync(item2);

        await salesService.RecordSaleAsync(item1.Id, quantity: 5);
        await salesService.RecordSaleAsync(item2.Id, quantity: 10);
        await salesService.RecordSaleAsync(item1.Id, quantity: 3);

        // Act
        var allSales = await salesService.GetAllSalesAsync();

        // Assert
        allSales.Should().HaveCount(3, "because 3 sales were recorded");
    }

    [Fact]
    public async Task GetSaleByIdAsync_ReturnsCorrectSale()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 10);
        sale.Should().NotBeNull();

        // Act
        var retrievedSale = await salesService.GetSaleByIdAsync(sale!.Id);

        // Assert
        retrievedSale.Should().NotBeNull();
        retrievedSale!.Id.Should().Be(sale.Id);
        retrievedSale.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task RecordSaleAsync_WithTransactionId_UsesProvidedTransactionId()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        var customTransactionId = Guid.NewGuid();

        // Act
        var sale = await salesService.RecordSaleAsync(
            item.Id,
            quantity: 10,
            transactionId: customTransactionId);

        // Assert
        sale.Should().NotBeNull();
        sale!.TransactionId.Should().Be(customTransactionId, "because custom transaction ID should be used");
    }

    [Fact]
    public async Task RecordSaleAsync_CalculatesProfitCorrectly()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var item = TestDataBuilder.CreateInventoryItem(cost: 10.00m, salePrice: 25.00m, quantity: 100);
        await inventoryService.CreateOrUpdateItemAsync(item);

        // Act
        var sale = await salesService.RecordSaleAsync(item.Id, quantity: 5);

        // Assert
        sale.Should().NotBeNull();
        sale!.Revenue.Should().Be(125.00m); // 5 * $25
        sale.COGS.Should().Be(50.00m); // 5 * $10
        sale.Profit.Should().Be(75.00m); // $125 - $50
        sale.ProfitMargin.Should().Be(60.00m); // ($75 / $125) * 100
    }

    [Fact]
    public async Task DeleteSaleAsync_WithNonExistentSale_ThrowsInvalidOperationException()
    {
        // Arrange
        var dataService = new InMemoryDataService();
        await dataService.InitializeAsync();

        var inventoryService = new InventoryService(dataService);
        var salesService = new SalesService(dataService, inventoryService, new BomService(dataService));

        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var action = async () => await salesService.DeleteSaleAsync(nonExistentId, restoreInventory: true);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
