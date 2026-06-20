using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.Tests.TestUtilities;

/// <summary>
/// Generates realistic test data for load testing and demos.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Random _random = new Random(42); // Fixed seed for reproducibility

    private static readonly string[] _productNames = new[]
    {
        "Widget", "Gadget", "Device", "Tool", "Component",
        "Accessory", "Part", "Module", "Unit", "Assembly"
    };

    private static readonly string[] _adjectives = new[]
    {
        "Premium", "Deluxe", "Standard", "Basic", "Pro",
        "Mini", "Maxi", "Ultra", "Super", "Mega"
    };

    private static readonly string[] _categories = new[]
    {
        "Electronics", "Hardware", "Software", "Accessories",
        "Components", "Tools", "Supplies", "Equipment"
    };

    private static readonly string[] _expenseCategories = new[]
    {
        "Rent", "Utilities", "Marketing", "Supplies",
        "Shipping", "Insurance", "Professional Services", "Equipment"
    };

    private static readonly string[] _customerNames = new[]
    {
        "John Smith", "Jane Doe", "Bob Johnson", "Alice Williams",
        "Charlie Brown", "Diana Prince", "Eve Adams", "Frank Miller"
    };

    /// <summary>
    /// Generates a specified number of inventory items.
    /// </summary>
    public static async Task<List<InventoryItem>> GenerateInventoryItemsAsync(
        IDataService dataService,
        int count = 100)
    {
        var items = new List<InventoryItem>();

        for (int i = 0; i < count; i++)
        {
            var costPerUnit = (decimal)(_random.NextDouble() * 90 + 10); // $10-$100
            var markup = (decimal)(_random.NextDouble() * 0.5 + 1.3); // 1.3x-1.8x markup

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = $"{_adjectives[_random.Next(_adjectives.Length)]} {_productNames[_random.Next(_productNames.Length)]} #{i + 1}",
                Sku = $"SKU{1000 + i}",
                Category = _categories[_random.Next(_categories.Length)],
                CostPerUnit = Math.Round(costPerUnit, 2),
                SalePrice = Math.Round(costPerUnit * markup, 2),
                QuantityOnHand = _random.Next(0, 200),
                MinimumStockLevel = _random.Next(5, 20),
                CreatedDate = DateTime.Now.AddDays(-_random.Next(1, 365)),
                LastModifiedDate = DateTime.Now
            };

            await dataService.SaveAsync(item);
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Generates sales for existing inventory items.
    /// </summary>
    public static async Task<List<Sale>> GenerateSalesAsync(
        IDataService dataService,
        List<InventoryItem> items,
        int count = 1000,
        int daysBack = 90)
    {
        var sales = new List<Sale>();

        for (int i = 0; i < count; i++)
        {
            var item = items[_random.Next(items.Count)];
            var quantity = _random.Next(1, 10);
            var saleDate = DateTime.Now.AddDays(-_random.Next(0, daysBack));

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                ItemName = item.Name,
                Quantity = quantity,
                SalePricePerUnit = item.SalePrice,
                CostPerUnit = item.CostPerUnit,
                SaleDate = saleDate,
                CustomerName = _random.Next(2) == 0 ? _customerNames[_random.Next(_customerNames.Length)] : null,
                Notes = _random.Next(5) == 0 ? "Test sale with notes" : null
            };

            await dataService.SaveAsync(sale);
            sales.Add(sale);
        }

        return sales;
    }

    /// <summary>
    /// Generates expenses over a date range.
    /// </summary>
    public static async Task<List<Expense>> GenerateExpensesAsync(
        IDataService dataService,
        int count = 200,
        int daysBack = 90)
    {
        var expenses = new List<Expense>();

        for (int i = 0; i < count; i++)
        {
            var amount = (decimal)(_random.NextDouble() * 900 + 100); // $100-$1000

            var expense = new Expense
            {
                Id = Guid.NewGuid(),
                Amount = Math.Round(amount, 2),
                Category = _expenseCategories[_random.Next(_expenseCategories.Length)],
                Description = $"Test expense #{i + 1}",
                ExpenseDate = DateTime.Now.AddDays(-_random.Next(0, daysBack)),
                CreatedDate = DateTime.Now
            };

            await dataService.SaveAsync(expense);
            expenses.Add(expense);
        }

        return expenses;
    }

    /// <summary>
    /// Generates inventory adjustments for items.
    /// </summary>
    public static async Task<List<InventoryAdjustment>> GenerateAdjustmentsAsync(
        IDataService dataService,
        List<InventoryItem> items,
        int count = 50,
        int daysBack = 90)
    {
        var adjustments = new List<InventoryAdjustment>();
        var reasons = Enum.GetValues<AdjustmentReason>();

        for (int i = 0; i < count; i++)
        {
            var item = items[_random.Next(items.Count)];
            var reason = reasons[_random.Next(reasons.Length)];

            // Negative for losses (Damaged, Lost, Expired), positive for gains (Found, Correction)
            var quantityChange = reason switch
            {
                AdjustmentReason.Damaged => -_random.Next(1, 10),
                AdjustmentReason.Lost => -_random.Next(1, 5),
                AdjustmentReason.Expired => -_random.Next(1, 20),
                AdjustmentReason.Found => _random.Next(1, 5),
                AdjustmentReason.CustomerReturn => _random.Next(1, 3),
                _ => _random.Next(-5, 5)
            };

            var adjustment = new InventoryAdjustment
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                InventoryItemName = item.Name,
                Reason = reason,
                QuantityChange = quantityChange,
                CostPerUnit = item.CostPerUnit,
                SalePricePerUnit = item.SalePrice,
                AdjustmentDate = DateTime.Now.AddDays(-_random.Next(0, daysBack)),
                CreatedDate = DateTime.Now,
                Notes = $"Test adjustment: {reason}"
            };

            await dataService.SaveAsync(adjustment);
            adjustments.Add(adjustment);
        }

        return adjustments;
    }

    /// <summary>
    /// Generates a complete test dataset with items, sales, expenses, and adjustments.
    /// </summary>
    public static async Task<TestDataSet> GenerateCompleteDatasetAsync(
        IDataService dataService,
        int itemCount = 100,
        int salesCount = 1000,
        int expenseCount = 200,
        int adjustmentCount = 50)
    {
        Console.WriteLine($"Generating {itemCount} inventory items...");
        var items = await GenerateInventoryItemsAsync(dataService, itemCount);

        Console.WriteLine($"Generating {salesCount} sales...");
        var sales = await GenerateSalesAsync(dataService, items, salesCount);

        Console.WriteLine($"Generating {expenseCount} expenses...");
        var expenses = await GenerateExpensesAsync(dataService, expenseCount);

        Console.WriteLine($"Generating {adjustmentCount} adjustments...");
        var adjustments = await GenerateAdjustmentsAsync(dataService, items, adjustmentCount);

        Console.WriteLine("Test data generation complete!");

        return new TestDataSet
        {
            Items = items,
            Sales = sales,
            Expenses = expenses,
            Adjustments = adjustments
        };
    }
}

public class TestDataSet
{
    public List<InventoryItem> Items { get; set; } = new();
    public List<Sale> Sales { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<InventoryAdjustment> Adjustments { get; set; } = new();
}
