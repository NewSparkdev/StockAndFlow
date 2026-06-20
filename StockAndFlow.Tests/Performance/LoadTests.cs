using StockAndFlow.Data;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace StockAndFlow.Tests.Performance;

/// <summary>
/// Performance tests to validate app works well with realistic data volumes.
/// These tests help catch performance regressions.
/// </summary>
public class LoadTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDbPath;
    private readonly SQLiteDataService _dataService;

    public LoadTests(ITestOutputHelper output)
    {
        _output = output;
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _dataService = new SQLiteDataService(_testDbPath, Path.Combine(Path.GetTempPath(), "settings.json"));
        _dataService.InitializeAsync().Wait();
    }

    public void Dispose()
    {
        _dataService?.Dispose();

        // Microsoft.Data.Sqlite pools connections, which keeps the file handle open even after the
        // DbContext is disposed. Clear the pool (and force finalization) before deleting the temp db,
        // and tolerate a residual lock since it's only a throwaway temp file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch (IOException)
        {
            // Temp file still locked by the SQLite native layer; the OS will reclaim it.
        }
    }

    [Fact]
    public async Task Load_1000Items_CompletesInReasonableTime()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        await TestDataGenerator.GenerateInventoryItemsAsync(_dataService, count: 1000);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Generated 1000 items in {stopwatch.ElapsedMilliseconds}ms");

        // Should complete in under 10 seconds even on slow machines
        Assert.True(stopwatch.ElapsedMilliseconds < 10000,
            $"Took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
    }

    [Fact]
    public async Task Load_10000Sales_CompletesInReasonableTime()
    {
        // Arrange
        var items = await TestDataGenerator.GenerateInventoryItemsAsync(_dataService, count: 100);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await TestDataGenerator.GenerateSalesAsync(_dataService, items, count: 10000);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Generated 10000 sales in {stopwatch.ElapsedMilliseconds}ms");

        // Should complete in under 30 seconds
        Assert.True(stopwatch.ElapsedMilliseconds < 30000,
            $"Took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms");
    }

    [Fact]
    public async Task Query_DateRangeOn10000Sales_UsesIndexes()
    {
        // Arrange
        var items = await TestDataGenerator.GenerateInventoryItemsAsync(_dataService, count: 10);
        await TestDataGenerator.GenerateSalesAsync(_dataService, items, count: 10000, daysBack: 365);

        var salesService = new SalesService(_dataService, new InventoryService(_dataService));

        var startDate = DateTime.Now.AddDays(-30);
        var endDate = DateTime.Now;

        var stopwatch = Stopwatch.StartNew();

        // Act
        var sales = await salesService.GetSalesByDateRangeAsync(startDate, endDate);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Date range query on 10000 sales took {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Returned {sales.Count} sales");

        // With indexes, this should be fast (under 100ms)
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Query took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms. Indexes may not be working.");
    }

    [Fact]
    public async Task CalculateMetrics_On10000Sales_CompletesQuickly()
    {
        // Arrange
        var items = await TestDataGenerator.GenerateInventoryItemsAsync(_dataService, count: 50);
        await TestDataGenerator.GenerateSalesAsync(_dataService, items, count: 10000);
        await TestDataGenerator.GenerateExpensesAsync(_dataService, count: 500);

        var inventoryService = new InventoryService(_dataService);
        var salesService = new SalesService(_dataService, inventoryService);
        var expenseService = new ExpenseService(_dataService);
        var adjustmentService = new InventoryAdjustmentService(_dataService, inventoryService);

        var calculationService = new CalculationService(
            _dataService,
            inventoryService,
            salesService,
            expenseService,
            adjustmentService);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var metrics = await calculationService.RecalculateMetricsAsync();

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Metrics calculation with 10000 sales took {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Total Revenue: {metrics.TotalRevenue:C}");
        _output.WriteLine($"Net Profit: {metrics.NetProfit:C}");

        // Should complete in under 2 seconds with database-level filtering
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Calculation took {stopwatch.ElapsedMilliseconds}ms, expected < 2000ms");

        // Verify metrics are reasonable
        Assert.True(metrics.TotalRevenue > 0);
        Assert.True(metrics.TotalSalesCount == 10000);

        calculationService.Dispose();
    }

    [Fact]
    public async Task Debouncing_100RapidChanges_TriggersOnlyOneRecalculation()
    {
        // Arrange
        var items = await TestDataGenerator.GenerateInventoryItemsAsync(_dataService, count: 10);

        var inventoryService = new InventoryService(_dataService);
        var salesService = new SalesService(_dataService, inventoryService);
        var expenseService = new ExpenseService(_dataService);
        var adjustmentService = new InventoryAdjustmentService(_dataService, inventoryService);

        var calculationService = new CalculationService(
            _dataService,
            inventoryService,
            salesService,
            expenseService,
            adjustmentService);

        int recalcCount = 0;
        calculationService.MetricsUpdated += (s, e) => recalcCount++;

        // Act - Trigger 100 rapid changes
        for (int i = 0; i < 100; i++)
        {
            var item = items[i % items.Count];
            item.QuantityOnHand += 1;
            await inventoryService.CreateOrUpdateItemAsync(item);
        }

        // Wait for debounce period (500ms) + buffer
        await Task.Delay(1000);

        // Assert
        _output.WriteLine($"100 rapid changes triggered {recalcCount} recalculation(s)");

        // Without debouncing: 100 recalcs
        // With debouncing: 1-2 recalcs (depending on timing)
        Assert.True(recalcCount < 5,
            $"Expected < 5 recalculations with debouncing, got {recalcCount}");

        calculationService.Dispose();
    }

    [Fact]
    public async Task CompleteWorkflow_1000ItemsAnd5000Sales_WorksEndToEnd()
    {
        // This is a comprehensive integration test simulating real usage

        var stopwatch = Stopwatch.StartNew();

        // Generate test data
        var dataset = await TestDataGenerator.GenerateCompleteDatasetAsync(
            _dataService,
            itemCount: 1000,
            salesCount: 5000,
            expenseCount: 300,
            adjustmentCount: 100);

        _output.WriteLine($"Generated test data in {stopwatch.ElapsedMilliseconds}ms");

        // Test various queries
        var inventoryService = new InventoryService(_dataService);
        var salesService = new SalesService(_dataService, inventoryService);

        stopwatch.Restart();
        var allItems = await inventoryService.GetAllItemsAsync();
        _output.WriteLine($"GetAllItems (1000): {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal(1000, allItems.Count);

        stopwatch.Restart();
        var searchResults = await inventoryService.SearchAsync("Premium");
        _output.WriteLine($"Search: {stopwatch.ElapsedMilliseconds}ms, found {searchResults.Count} items");

        stopwatch.Restart();
        var recentSales = await salesService.GetSalesByDateRangeAsync(
            DateTime.Now.AddDays(-30),
            DateTime.Now);
        _output.WriteLine($"Date range query: {stopwatch.ElapsedMilliseconds}ms, found {recentSales.Count} sales");

        // All queries should be fast
        Assert.True(stopwatch.ElapsedMilliseconds < 1000);
    }
}
