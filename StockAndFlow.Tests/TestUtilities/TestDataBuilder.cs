using StockAndFlow.Models;

namespace StockAndFlow.Tests.TestUtilities;

/// <summary>
/// Provides factory methods for creating test data with sensible defaults.
/// Makes test setup code more concise and readable.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates an InventoryItem with default values.
    /// All parameters are optional and override the defaults.
    /// </summary>
    public static InventoryItem CreateInventoryItem(
        string? name = null,
        decimal cost = 10.00m,
        decimal salePrice = 20.00m,
        decimal quantity = 100,
        string? sku = null,
        string? category = null,
        decimal minimumStockLevel = 5,
        string unitOfMeasure = "each")
    {
        var id = Guid.NewGuid();
        return new InventoryItem
        {
            Id = id,
            Name = name ?? "Test Item",
            Sku = sku ?? $"SKU-{id.ToString()[..8]}",
            Category = category ?? "Test Category",
            CostPerUnit = cost,
            SalePrice = salePrice,
            QuantityOnHand = quantity,
            MinimumStockLevel = minimumStockLevel,
            UnitOfMeasure = unitOfMeasure,
            Supplier = "Test Supplier",
            Notes = "Test item created for unit testing",
            CreatedDate = DateTime.Now.AddDays(-30),
            LastModifiedDate = DateTime.Now
        };
    }

    /// <summary>
    /// Creates a Sale with default values.
    /// </summary>
    public static Sale CreateSale(
        Guid? itemId = null,
        string? itemName = null,
        decimal quantity = 1,
        decimal salePrice = 20.00m,
        decimal cost = 10.00m,
        string? customerName = null,
        string? customerEmail = null,
        DateTime? saleDate = null,
        Guid? transactionId = null)
    {
        return new Sale
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId ?? Guid.NewGuid(),
            InventoryItemId = itemId ?? Guid.NewGuid(),
            ItemName = itemName ?? "Test Item",
            Quantity = quantity,
            SalePricePerUnit = salePrice,
            CostPerUnit = cost,
            CustomerName = customerName ?? "Test Customer",
            CustomerEmail = customerEmail ?? "test@example.com",
            SaleDate = saleDate ?? DateTime.Now,
            Notes = "Test sale"
        };
    }

    /// <summary>
    /// Creates an Expense with default values.
    /// </summary>
    public static Expense CreateExpense(
        decimal amount = 100.00m,
        string? category = null,
        string? description = null,
        Guid? inventoryItemId = null,
        DateTime? expenseDate = null)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Category = category ?? "Operating Expenses",
            Description = description ?? "Test expense",
            InventoryItemId = inventoryItemId,
            ExpenseDate = expenseDate ?? DateTime.Now,
            CreatedDate = DateTime.Now
        };
    }

    /// <summary>
    /// Creates an InventoryAdjustment with default values.
    /// </summary>
    public static InventoryAdjustment CreateAdjustment(
        Guid? itemId = null,
        string? itemName = null,
        decimal quantityChange = -5,
        AdjustmentReason reason = AdjustmentReason.Damaged,
        decimal cost = 10.00m,
        decimal salePrice = 20.00m,
        string? notes = null,
        DateTime? adjustmentDate = null)
    {
        return new InventoryAdjustment
        {
            Id = Guid.NewGuid(),
            InventoryItemId = itemId ?? Guid.NewGuid(),
            InventoryItemName = itemName ?? "Test Item",
            QuantityChange = quantityChange,
            Reason = reason,
            CostPerUnit = cost,
            SalePricePerUnit = salePrice,
            Notes = notes ?? "Test adjustment",
            AdjustmentDate = adjustmentDate ?? DateTime.Now,
            CreatedDate = DateTime.Now
        };
    }

    /// <summary>
    /// Creates a SaleTransaction (shopping cart) with multiple items.
    /// </summary>
    public static SaleTransaction CreateSaleTransaction(
        int itemCount = 2,
        string? customerName = null,
        string? customerEmail = null,
        DateTime? saleDate = null)
    {
        var transactionId = Guid.NewGuid();
        var transaction = new SaleTransaction
        {
            TransactionId = transactionId,
            CustomerName = customerName ?? "Test Customer",
            CustomerEmail = customerEmail ?? "test@example.com",
            SaleDate = saleDate ?? DateTime.Now,
            Notes = "Test transaction"
        };

        // Create sale items for the transaction
        var items = new List<Sale>();
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(CreateSale(
                itemName: $"Test Item {i + 1}",
                quantity: i + 1,
                salePrice: 20.00m + (i * 5),
                cost: 10.00m + (i * 2),
                customerName: transaction.CustomerName,
                customerEmail: transaction.CustomerEmail,
                saleDate: transaction.SaleDate,
                transactionId: transactionId));
        }

        transaction.Items = items;
        return transaction;
    }

    /// <summary>
    /// Creates a BusinessSettings object with default test values.
    /// </summary>
    public static BusinessSettings CreateBusinessSettings(
        string? businessName = null,
        string? email = null,
        string? phone = null)
    {
        return new BusinessSettings
        {
            Id = Guid.NewGuid(),
            BusinessName = businessName ?? "Test Business",
            Address = "123 Test Street",
            City = "Test City",
            State = "NY",
            ZipCode = "12345",
            Phone = phone ?? "(555) 123-4567",
            Email = email ?? "test@business.com",
            Website = "www.testbusiness.com",
            TaxId = "12-3456789",
            LastModified = DateTime.Now
        };
    }

    /// <summary>
    /// Creates an AppSettings object with default test values.
    /// </summary>
    public static AppSettings CreateAppSettings(
        bool allowNegativeInventory = false,
        StorageMode storageMode = StorageMode.SQLite)
    {
        return new AppSettings
        {
            AllowNegativeInventory = allowNegativeInventory,
            StorageMode = storageMode,
            DataStoragePath = "TestData",
            ImageStoragePath = "TestImages",
            ShopifyEnabled = false
        };
    }
}
