using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.TestUtilities;

/// <summary>
/// Optional base class for service tests providing common test infrastructure.
/// Tests can either inherit from this or create their own instances directly.
/// </summary>
public abstract class ServiceTestBase : IDisposable
{
    protected InMemoryDataService DataService { get; private set; }
    protected AppSettings Settings { get; private set; }

    protected ServiceTestBase()
    {
        DataService = new InMemoryDataService();
        Settings = new AppSettings
        {
            AllowNegativeInventory = false,
            StorageMode = StorageMode.SQLite,
            DataStoragePath = "TestData",
            ImageStoragePath = "TestImages"
        };

        // Initialize the data service
        DataService.InitializeAsync().Wait();

        // Set default settings
        DataService.SaveSettingsAsync(Settings).Wait();
    }

    /// <summary>
    /// Updates the AllowNegativeInventory setting for the test.
    /// </summary>
    protected void SetAllowNegativeInventory(bool allow)
    {
        Settings.AllowNegativeInventory = allow;
        DataService.SaveSettingsAsync(Settings).Wait();
    }

    /// <summary>
    /// Resets the data service to a clean state.
    /// Useful when running multiple tests in sequence.
    /// </summary>
    protected void ResetDataService()
    {
        DataService.Reset();
        DataService.InitializeAsync().Wait();

        // Restore default settings
        Settings = new AppSettings
        {
            AllowNegativeInventory = false,
            StorageMode = StorageMode.SQLite
        };
        DataService.SaveSettingsAsync(Settings).Wait();
    }

    /// <summary>
    /// Creates a new InventoryService instance using the test data service.
    /// </summary>
    protected InventoryService CreateInventoryService()
    {
        return new InventoryService(DataService);
    }

    /// <summary>
    /// Creates a new SalesService instance using the test data service.
    /// </summary>
    protected SalesService CreateSalesService()
    {
        var inventoryService = CreateInventoryService();
        return new SalesService(DataService, inventoryService, new BomService(DataService));
    }

    /// <summary>
    /// Creates a new ExpenseService instance using the test data service.
    /// </summary>
    protected ExpenseService CreateExpenseService()
    {
        return new ExpenseService(DataService);
    }

    /// <summary>
    /// Creates a new InventoryAdjustmentService instance using the test data service.
    /// </summary>
    protected InventoryAdjustmentService CreateInventoryAdjustmentService()
    {
        var inventoryService = CreateInventoryService();
        return new InventoryAdjustmentService(DataService, inventoryService);
    }

    /// <summary>
    /// Creates a new CalculationService instance using the test data service.
    /// </summary>
    protected CalculationService CreateCalculationService()
    {
        var inventoryService = CreateInventoryService();
        var salesService = CreateSalesService();
        var expenseService = CreateExpenseService();
        var adjustmentService = CreateInventoryAdjustmentService();

        return new CalculationService(
            DataService,
            inventoryService,
            salesService,
            expenseService,
            adjustmentService);
    }

    public virtual void Dispose()
    {
        // Cleanup if needed
    }
}
