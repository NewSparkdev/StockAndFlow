using Microsoft.Data.Sqlite;
using StockAndFlow.Data;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Migrations;

/// <summary>
/// Upgrade-safety tests: a database created by an older version of the app (whole-number
/// quantities, no UnitOfMeasure/IsDeleted columns, no BomComponents table) must migrate
/// cleanly and keep every value intact. This is the scariest failure mode the app has —
/// a tester updates and their inventory is corrupted — so it gets real-file SQLite tests.
/// </summary>
public class DatabaseMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public DatabaseMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"stockandflow-migration-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* best effort */ }
    }

    /// <summary>
    /// Creates a database with the ORIGINAL desktop-release schema (initial commit): INTEGER
    /// quantity columns, no IsDeleted/DeletedDate/UnitOfMeasure columns on InventoryItems,
    /// no BomComponents table, no Customers table, and no CustomerId column on Sales
    /// (CustomerName/Email existed from day one; the Customer entity came later).
    /// </summary>
    private async Task<Guid> CreateLegacyDatabaseAsync(int quantityOnHand = 90, int saleQuantity = 3)
    {
        var itemId = Guid.NewGuid();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE InventoryItems (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                Sku TEXT NULL,
                Category TEXT NULL,
                CostPerUnit TEXT NOT NULL,
                SalePrice TEXT NOT NULL,
                QuantityOnHand INTEGER NOT NULL,
                MinimumStockLevel INTEGER NOT NULL,
                Supplier TEXT NULL,
                Notes TEXT NULL,
                ImagePath TEXT NOT NULL,
                CreatedDate TEXT NOT NULL,
                LastModifiedDate TEXT NOT NULL,
                ShopifyProductId TEXT NULL,
                ShopifyVariantId TEXT NULL,
                LastSyncedAt TEXT NULL
            );
            CREATE TABLE Sales (
                Id TEXT NOT NULL PRIMARY KEY,
                TransactionId TEXT NOT NULL,
                SaleDate TEXT NOT NULL,
                InventoryItemId TEXT NOT NULL,
                ItemName TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                SalePricePerUnit TEXT NOT NULL,
                CostPerUnit TEXT NOT NULL,
                ShopifyOrderId TEXT NULL,
                ShopifyOrderNumber TEXT NULL,
                CustomerName TEXT NULL,
                CustomerEmail TEXT NULL,
                Notes TEXT NULL,
                TaxStateCode TEXT NULL,
                TaxRate TEXT NOT NULL,
                TaxAmount TEXT NOT NULL
            );
            CREATE TABLE Expenses (
                Id TEXT NOT NULL PRIMARY KEY,
                ExpenseDate TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Category TEXT NOT NULL,
                Description TEXT NULL,
                ReceiptImagePath TEXT NULL,
                InventoryItemId TEXT NULL,
                Notes TEXT NULL,
                CreatedDate TEXT NOT NULL
            );
            CREATE TABLE InventoryAdjustments (
                Id TEXT NOT NULL PRIMARY KEY,
                InventoryItemId TEXT NOT NULL,
                InventoryItemName TEXT NOT NULL,
                Reason TEXT NOT NULL,
                QuantityChange INTEGER NOT NULL,
                CostPerUnit TEXT NOT NULL,
                SalePricePerUnit TEXT NOT NULL,
                Notes TEXT NULL,
                AdjustmentDate TEXT NOT NULL,
                CreatedDate TEXT NOT NULL
            );
            CREATE TABLE BusinessSettings (
                Id TEXT NOT NULL PRIMARY KEY,
                BusinessName TEXT NULL,
                Address TEXT NULL,
                City TEXT NULL,
                State TEXT NULL,
                ZipCode TEXT NULL,
                Phone TEXT NULL,
                Email TEXT NULL,
                Website TEXT NULL,
                TaxId TEXT NULL,
                DefaultTaxStateCode TEXT NULL,
                LogoPath TEXT NULL,
                LastModified TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync();

        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO InventoryItems
                (Id, Name, Sku, Category, CostPerUnit, SalePrice, QuantityOnHand, MinimumStockLevel,
                 Supplier, Notes, ImagePath, CreatedDate, LastModifiedDate)
            VALUES
                ($id, 'Legacy Widget', 'LEG-1', 'Legacy', '10.5', '20.0', $qty, 5,
                 'Old Supplier', 'made before the update', '', '2025-01-01 00:00:00', '2025-01-01 00:00:00');

            INSERT INTO Sales
                (Id, TransactionId, SaleDate, InventoryItemId, ItemName, Quantity,
                 SalePricePerUnit, CostPerUnit, TaxRate, TaxAmount)
            VALUES
                ($saleId, $txId, '2025-06-01 12:00:00', $id, 'Legacy Widget', $saleQty,
                 '20.0', '10.5', '0', '0');
            """;
        insert.Parameters.AddWithValue("$id", itemId.ToString().ToUpperInvariant());
        insert.Parameters.AddWithValue("$qty", quantityOnHand);
        insert.Parameters.AddWithValue("$saleId", Guid.NewGuid().ToString().ToUpperInvariant());
        insert.Parameters.AddWithValue("$txId", Guid.NewGuid().ToString().ToUpperInvariant());
        insert.Parameters.AddWithValue("$saleQty", saleQuantity);
        await insert.ExecuteNonQueryAsync();

        return itemId;
    }

    [Fact]
    public async Task Migration_LegacyDatabase_AllItemValuesSurvive()
    {
        var itemId = await CreateLegacyDatabaseAsync(quantityOnHand: 90);

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        using var context = new StockAndFlowDbContext(_dbPath);
        var item = context.InventoryItems.Single(i => i.Id == itemId);

        item.Name.Should().Be("Legacy Widget");
        item.QuantityOnHand.Should().Be(90m, "whole-number stock must read back exactly as a decimal");
        item.MinimumStockLevel.Should().Be(5m);
        item.CostPerUnit.Should().Be(10.5m);
        item.SalePrice.Should().Be(20m);
        item.UnitOfMeasure.Should().Be("each", "items from before the feature must default to counted");
        item.ExtraCostPerUnit.Should().Be(0m, "items from before the feature have no extra cost");
        item.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Migration_LegacySale_IntegerQuantityReadsAsDecimal()
    {
        await CreateLegacyDatabaseAsync(saleQuantity: 3);

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        using var context = new StockAndFlowDbContext(_dbPath);
        var sale = context.Sales.Single();
        sale.Quantity.Should().Be(3m);
        sale.SalePricePerUnit.Should().Be(20m);
    }

    [Fact]
    public async Task Migration_ThenFractionalUpdate_RoundTripsThroughLegacyColumns()
    {
        var itemId = await CreateLegacyDatabaseAsync();
        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        // Write a fractional amount + unit into columns that were created as INTEGER
        using (var context = new StockAndFlowDbContext(_dbPath))
        {
            var item = context.InventoryItems.Single(i => i.Id == itemId);
            item.QuantityOnHand = 90.5m;
            item.MinimumStockLevel = 2.25m;
            item.UnitOfMeasure = "oz";
            context.SaveChanges();
        }

        using (var context = new StockAndFlowDbContext(_dbPath))
        {
            var item = context.InventoryItems.Single(i => i.Id == itemId);
            item.QuantityOnHand.Should().Be(90.5m, "fractional stock must survive a full write/read cycle");
            item.MinimumStockLevel.Should().Be(2.25m);
            item.UnitOfMeasure.Should().Be("oz");
        }
    }

    [Fact]
    public async Task Migration_RunTwice_IsIdempotent()
    {
        var itemId = await CreateLegacyDatabaseAsync();

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);
        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath); // must not throw or duplicate

        using var context = new StockAndFlowDbContext(_dbPath);
        context.InventoryItems.Single(i => i.Id == itemId).QuantityOnHand.Should().Be(90m);
    }

    [Fact]
    public async Task Migration_AddsCustomerIdToSales_AndSalesStillLoad()
    {
        await CreateLegacyDatabaseAsync(saleQuantity: 3);

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        // This exact query failed with "no such column: s.CustomerId" before the migration
        // handled the customer feature.
        using var context = new StockAndFlowDbContext(_dbPath);
        var sale = context.Sales.Single();
        sale.CustomerId.Should().BeNull("pre-customer sales have no linked customer");
        sale.Quantity.Should().Be(3m);
    }

    [Fact]
    public async Task Migration_CreatesCustomersTable()
    {
        await CreateLegacyDatabaseAsync();

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        using var context = new StockAndFlowDbContext(_dbPath);
        var customer = new StockAndFlow.Models.Customer { Name = "First Customer", Email = "c@example.com" };
        context.Customers.Add(customer);
        context.SaveChanges();

        context.Customers.Single(c => c.Id == customer.Id).Name.Should().Be("First Customer");
    }

    [Fact]
    public async Task Migration_AddsInvoiceDisplayColumns_DefaultingToShown()
    {
        await CreateLegacyDatabaseAsync();

        using (var seed = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await seed.OpenAsync();
            var insert = seed.CreateCommand();
            insert.CommandText = """
                INSERT INTO BusinessSettings (Id, BusinessName, LastModified)
                VALUES ($id, 'Legacy Candle Co', '2025-01-01 00:00:00');
                """;
            insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString().ToUpperInvariant());
            await insert.ExecuteNonQueryAsync();
        }

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        using var context = new StockAndFlowDbContext(_dbPath);
        var settings = context.BusinessSettings.Single();
        settings.BusinessName.Should().Be("Legacy Candle Co");
        settings.ShowLogoOnInvoice.Should().BeTrue("existing invoices must keep looking the same");
        settings.ShowPhoneOnInvoice.Should().BeTrue();
        settings.ShowEmailOnInvoice.Should().BeTrue();
        settings.ShowWebsiteOnInvoice.Should().BeTrue();
        settings.ShowTaxIdOnInvoice.Should().BeTrue();
    }

    [Fact]
    public async Task Migration_CreatesBomComponentsTable()
    {
        await CreateLegacyDatabaseAsync();

        await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_dbPath);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='BomComponents'";
        Convert.ToInt32(await cmd.ExecuteScalarAsync()).Should().Be(1);
    }

    [Fact]
    public void FreshDatabase_MeasuredItem_RoundTripsExactly()
    {
        using (var context = new StockAndFlowDbContext(_dbPath))
        {
            context.EnsureDatabaseCreated();
            context.InventoryItems.Add(TestDataBuilder.CreateInventoryItem(
                "Wax", cost: 0.30m, salePrice: 0.75m, quantity: 90.5m, unitOfMeasure: "oz"));
            context.SaveChanges();
        }

        using (var context = new StockAndFlowDbContext(_dbPath))
        {
            var wax = context.InventoryItems.Single();
            wax.QuantityOnHand.Should().Be(90.5m);
            wax.UnitOfMeasure.Should().Be("oz");
            wax.CostPerUnit.Should().Be(0.30m);
        }
    }
}
