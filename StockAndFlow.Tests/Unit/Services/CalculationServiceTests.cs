using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;
using Xunit;

namespace StockAndFlow.Tests.Unit.Services;

public class CalculationServiceTests : IDisposable
{
    private readonly InMemoryDataService _dataService;
    private readonly InventoryService _inventoryService;
    private readonly SalesService _salesService;
    private readonly ExpenseService _expenseService;
    private readonly InventoryAdjustmentService _adjustmentService;
    private readonly CalculationService _calculationService;

    public CalculationServiceTests()
    {
        _dataService = new InMemoryDataService();
        _dataService.InitializeAsync().Wait();

        _inventoryService = new InventoryService(_dataService);
        _salesService = new SalesService(_dataService, _inventoryService, new BomService(_dataService));
        _expenseService = new ExpenseService(_dataService);
        _adjustmentService = new InventoryAdjustmentService(_dataService, _inventoryService);

        _calculationService = new CalculationService(
            _dataService,
            _inventoryService,
            _salesService,
            _expenseService,
            _adjustmentService);
    }

    public void Dispose()
    {
        _calculationService?.Dispose();
    }

    [Fact]
    public async Task RecalculateMetrics_EmptyData_ReturnsZeroMetrics()
    {
        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(0, metrics.TotalRevenue);
        Assert.Equal(0, metrics.TotalCOGS);
        Assert.Equal(0, metrics.GrossProfit);
        Assert.Equal(0, metrics.NetProfit);
        Assert.Equal(0, metrics.TotalExpenses);
        Assert.Equal(0, metrics.TotalInventoryValue);
        Assert.Equal(0, metrics.TotalSalesCount);
    }

    [Fact]
    public async Task RecalculateMetrics_WithSales_CalculatesCorrectRevenue()
    {
        // Arrange
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CostPerUnit = 10m,
            SalePrice = 20m,
            QuantityOnHand = 100
        };
        await _inventoryService.CreateOrUpdateItemAsync(item);

        // Record 5 sales
        for (int i = 0; i < 5; i++)
        {
            await _salesService.RecordSaleAsync(item.Id, quantity: 2, customSalePrice: 20m);
        }

        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(200m, metrics.TotalRevenue); // 5 sales * 2 qty * $20
        Assert.Equal(100m, metrics.TotalCOGS);     // 5 sales * 2 qty * $10
        Assert.Equal(100m, metrics.GrossProfit);   // Revenue - COGS
        Assert.Equal(5, metrics.TotalSalesCount);
        Assert.Equal(10, metrics.TotalItemsSold);
    }

    [Fact]
    public async Task RecalculateMetrics_WithExpenses_CalculatesNetProfit()
    {
        // Arrange
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CostPerUnit = 10m,
            SalePrice = 20m,
            QuantityOnHand = 100
        };
        await _inventoryService.CreateOrUpdateItemAsync(item);

        // Record sale: Revenue $40, COGS $20, Gross Profit $20
        await _salesService.RecordSaleAsync(item.Id, quantity: 2, customSalePrice: 20m);

        // Record expense: $5
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Amount = 5m,
            Category = "Rent",
            ExpenseDate = DateTime.Now
        };
        await _expenseService.CreateOrUpdateExpenseAsync(expense);

        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(40m, metrics.TotalRevenue);
        Assert.Equal(20m, metrics.TotalCOGS);
        Assert.Equal(20m, metrics.GrossProfit);
        Assert.Equal(5m, metrics.TotalExpenses);
        Assert.Equal(15m, metrics.NetProfit); // Gross Profit - Expenses
    }

    [Fact]
    public async Task RecalculateMetrics_WithDateRange_FiltersCorrectly()
    {
        // Arrange
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CostPerUnit = 10m,
            SalePrice = 20m,
            QuantityOnHand = 100
        };
        await _inventoryService.CreateOrUpdateItemAsync(item);

        // Record sales on different dates
        var oldSale = new Sale
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            ItemName = item.Name,
            Quantity = 1,
            SalePricePerUnit = 20m,
            CostPerUnit = 10m,
            SaleDate = DateTime.Now.AddDays(-10)
        };
        await _dataService.SaveAsync(oldSale);

        var recentSale = new Sale
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            ItemName = item.Name,
            Quantity = 1,
            SalePricePerUnit = 20m,
            CostPerUnit = 10m,
            SaleDate = DateTime.Now.AddDays(-2)
        };
        await _dataService.SaveAsync(recentSale);

        // Act - Filter for last 5 days
        var startDate = DateTime.Now.AddDays(-5);
        var endDate = DateTime.Now;
        var metrics = await _calculationService.RecalculateMetricsAsync(startDate, endDate);

        // Assert - Should only include recent sale
        Assert.Equal(20m, metrics.TotalRevenue);
        Assert.Equal(10m, metrics.TotalCOGS);
        Assert.Equal(1, metrics.TotalSalesCount);
    }

    [Fact]
    public async Task RecalculateMetrics_WithInventoryLosses_ReducesNetProfit()
    {
        // Arrange
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CostPerUnit = 10m,
            SalePrice = 20m,
            QuantityOnHand = 100
        };
        await _inventoryService.CreateOrUpdateItemAsync(item);

        // Record sale: Gross Profit $20
        await _salesService.RecordSaleAsync(item.Id, quantity: 2, customSalePrice: 20m);

        // Record damaged goods: Loss of 3 items at $10 each = $30 loss
        var adjustment = new InventoryAdjustment
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            InventoryItemName = item.Name,
            Reason = AdjustmentReason.Damaged,
            QuantityChange = -3,
            CostPerUnit = 10m,
            SalePricePerUnit = 20m,
            AdjustmentDate = DateTime.Now,
            CreatedDate = DateTime.Now
        };
        await _dataService.SaveAsync(adjustment);

        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(40m, metrics.TotalRevenue);
        Assert.Equal(20m, metrics.GrossProfit);
        Assert.Equal(30m, metrics.TotalInventoryLosses);
        Assert.Equal(-10m, metrics.NetProfit); // $20 gross - $30 losses = -$10
    }

    [Fact]
    public async Task RecalculateMetrics_CalculatesInventoryValue()
    {
        // Arrange
        var item1 = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Item 1",
            CostPerUnit = 10m,
            QuantityOnHand = 5
        };
        var item2 = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Item 2",
            CostPerUnit = 25m,
            QuantityOnHand = 4
        };

        await _inventoryService.CreateOrUpdateItemAsync(item1);
        await _inventoryService.CreateOrUpdateItemAsync(item2);

        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(150m, metrics.TotalInventoryValue); // (10*5) + (25*4) = 150
        Assert.Equal(2, metrics.UniqueInventoryItems);
    }

    [Fact]
    public async Task RecalculateMetrics_CalculatesProfitMargin()
    {
        // Arrange
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CostPerUnit = 10m,
            SalePrice = 20m,
            QuantityOnHand = 100
        };
        await _inventoryService.CreateOrUpdateItemAsync(item);

        // Revenue: $100, COGS: $50, Gross: $50, Expenses: $10, Net: $40
        await _salesService.RecordSaleAsync(item.Id, quantity: 5, customSalePrice: 20m);

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Amount = 10m,
            Category = "Operating",
            ExpenseDate = DateTime.Now
        };
        await _expenseService.CreateOrUpdateExpenseAsync(expense);

        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(100m, metrics.TotalRevenue);
        Assert.Equal(40m, metrics.NetProfit);
        Assert.Equal(40m, metrics.OverallProfitMargin); // (40/100)*100 = 40%
    }

    [Fact]
    public async Task GetItemProfitability_CalculatesCorrectly()
    {
        // Arrange
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CostPerUnit = 10m,
            SalePrice = 20m,
            QuantityOnHand = 100
        };
        await _inventoryService.CreateOrUpdateItemAsync(item);

        // Record sales: Profit = (20-10) * 3 = $30
        await _salesService.RecordSaleAsync(item.Id, quantity: 3, customSalePrice: 20m);

        // Record item expense: $5
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Amount = 5m,
            Category = "Item Cost",
            ExpenseDate = DateTime.Now,
            InventoryItemId = item.Id
        };
        await _expenseService.CreateOrUpdateExpenseAsync(expense);

        // Act
        var profitability = await _calculationService.GetItemProfitabilityAsync(item.Id);

        // Assert
        Assert.Equal(25m, profitability); // $30 profit - $5 expense = $25
    }

    [Fact]
    public async Task RecalculateMetrics_WithZeroRevenue_HasZeroProfitMargin()
    {
        // Arrange - No sales, only expenses
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Amount = 100m,
            Category = "Rent",
            ExpenseDate = DateTime.Now
        };
        await _expenseService.CreateOrUpdateExpenseAsync(expense);

        // Act
        var metrics = await _calculationService.RecalculateMetricsAsync();

        // Assert
        Assert.Equal(0m, metrics.TotalRevenue);
        Assert.Equal(-100m, metrics.NetProfit);
        Assert.Equal(0m, metrics.OverallProfitMargin); // Should be 0, not NaN or infinity
    }
}
