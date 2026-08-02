using ClosedXML.Excel;
using FluentAssertions;
using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Export/Import is the app's backup-and-restore story ("your data is never hostage") and a
/// paywall lever, but had no test coverage at all. These tests run real ClosedXML round-trips
/// against real .xlsx files on disk.
/// </summary>
public class ExportImportServiceTests : IDisposable
{
    private readonly InMemoryDataService _dataService;
    private readonly InventoryService _inventoryService;
    private readonly SalesService _salesService;
    private readonly ExpenseService _expenseService;
    private readonly ExportImportService _service;
    private readonly List<string> _tempFiles = new();

    public ExportImportServiceTests()
    {
        _dataService = new InMemoryDataService();
        _dataService.InitializeAsync().Wait();
        _inventoryService = new InventoryService(_dataService);
        _salesService = new SalesService(_dataService, _inventoryService, new BomService(_dataService));
        _expenseService = new ExpenseService(_dataService);
        _service = new ExportImportService(_dataService, _inventoryService, _salesService, _expenseService);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { /* best effort */ }
        }
    }

    private string TempXlsx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sf-export-{Guid.NewGuid():N}.xlsx");
        _tempFiles.Add(path);
        return path;
    }

    private async Task<InventoryItem> SeedMeasuredItemAsync() =>
        await _inventoryService.CreateOrUpdateItemAsync(new InventoryItem
        {
            Name = "Soy Wax",
            Sku = "WAX-1",
            Category = "Materials",
            CostPerUnit = 0.30m,
            ExtraCostPerUnit = 0.15m,
            SalePrice = 0.75m,
            QuantityOnHand = 90.5m,
            MinimumStockLevel = 10.5m,
            UnitOfMeasure = "oz",
            Supplier = "Wax Supply Co",
            Notes = "Store below 80F"
        });

    [Fact]
    public async Task Export_WritesWorkbook_WithAllExpectedSheets()
    {
        await SeedMeasuredItemAsync();
        var path = TempXlsx();

        await _service.ExportToExcelAsync(path);

        File.Exists(path).Should().BeTrue();
        using var wb = new XLWorkbook(path);
        wb.Worksheets.Select(w => w.Name).Should()
            .Contain(new[] { "Inventory", "Sales", "Expenses", "Instructions" });
    }

    [Fact]
    public async Task Export_RejectsNonXlsxPath()
    {
        var act = async () => await _service.ExportToExcelAsync(
            Path.Combine(Path.GetTempPath(), $"sf-{Guid.NewGuid():N}.txt"));

        await act.Should().ThrowAsync<Exception>("only .xlsx output is supported");
    }

    [Fact]
    public async Task RoundTrip_PreservesEveryInventoryField()
    {
        var original = await SeedMeasuredItemAsync();
        var path = TempXlsx();
        await _service.ExportToExcelAsync(path);

        // Simulate restoring onto a clean install.
        _dataService.Reset();
        var result = await _service.ImportFromExcelAsync(path);
        result.Success.Should().BeTrue(result.ErrorMessage);

        var restored = (await _inventoryService.GetAllItemsAsync()).Single();
        restored.Id.Should().Be(original.Id, "the row carries its ID so restore updates in place");
        restored.Name.Should().Be("Soy Wax");
        restored.Sku.Should().Be("WAX-1");
        restored.Category.Should().Be("Materials");
        restored.CostPerUnit.Should().Be(0.30m);
        restored.SalePrice.Should().Be(0.75m);
        restored.QuantityOnHand.Should().Be(90.5m, "fractional stock must survive a backup");

        // Fields below were silently dropped by the original export — a "backup" that
        // quietly loses how an item is measured is worse than no backup.
        restored.UnitOfMeasure.Should().Be("oz", "measure-by-weight must survive a backup");
        restored.MinimumStockLevel.Should().Be(10.5m, "low-stock alerts must survive a backup");
        restored.ExtraCostPerUnit.Should().Be(0.15m, "labor/packaging cost must survive a backup");
        restored.Supplier.Should().Be("Wax Supply Co");
        restored.Notes.Should().Be("Store below 80F");
    }

    [Fact]
    public async Task RoundTrip_PreservesSales()
    {
        var item = await SeedMeasuredItemAsync();
        await _salesService.RecordSaleAsync(item.Id, 2.5m, 0.80m, "Jane Doe", "jane@example.com", "market stall");
        var path = TempXlsx();
        await _service.ExportToExcelAsync(path);

        _dataService.Reset();
        var result = await _service.ImportFromExcelAsync(path);
        result.Success.Should().BeTrue(result.ErrorMessage);

        var sale = (await _salesService.GetAllSalesAsync()).Single();
        sale.ItemName.Should().Be("Soy Wax");
        sale.Quantity.Should().Be(2.5m, "fractional sale quantities must survive a backup");
        sale.SalePricePerUnit.Should().Be(0.80m);
        sale.CustomerName.Should().Be("Jane Doe");
        sale.Notes.Should().Be("market stall");
        sale.UnitOfMeasure.Should().Be("oz", "a restored invoice must still read '2.5 oz'");
    }

    [Fact]
    public async Task RoundTrip_PreservesExpenses()
    {
        await _expenseService.CreateOrUpdateExpenseAsync(new Expense
        {
            ExpenseDate = new DateTime(2026, 7, 1),
            Category = "Supplies",
            Amount = 42.50m,
            Description = "Wick spools"
        });
        var path = TempXlsx();
        await _service.ExportToExcelAsync(path);

        _dataService.Reset();
        var result = await _service.ImportFromExcelAsync(path);
        result.Success.Should().BeTrue(result.ErrorMessage);

        var expense = (await _expenseService.GetAllExpensesAsync()).Single();
        expense.Category.Should().Be("Supplies");
        expense.Amount.Should().Be(42.50m);
        expense.Description.Should().Be("Wick spools");
    }

    [Fact]
    public async Task Import_SameFileTwice_DoesNotDuplicateInventory()
    {
        await SeedMeasuredItemAsync();
        var path = TempXlsx();
        await _service.ExportToExcelAsync(path);

        await _service.ImportFromExcelAsync(path);
        await _service.ImportFromExcelAsync(path);

        (await _inventoryService.GetAllItemsAsync()).Should().HaveCount(1,
            "rows carry their ID, so re-importing updates rather than duplicates");
    }

    [Fact]
    public async Task Import_MissingFile_FailsCleanly()
    {
        var result = await _service.ImportFromExcelAsync(
            Path.Combine(Path.GetTempPath(), $"sf-missing-{Guid.NewGuid():N}.xlsx"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Import_GarbageFile_FailsCleanlyWithoutCorruptingData()
    {
        await SeedMeasuredItemAsync();
        var path = TempXlsx();
        await File.WriteAllTextAsync(path, "this is not a spreadsheet");

        var result = await _service.ImportFromExcelAsync(path);

        result.Success.Should().BeFalse();
        (await _inventoryService.GetAllItemsAsync()).Should().HaveCount(1,
            "a failed import must leave existing data untouched");
    }
}
