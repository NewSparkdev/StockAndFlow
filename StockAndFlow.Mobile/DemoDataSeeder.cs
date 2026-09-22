using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile;

/// <summary>
/// Populates the database with a fictitious candle business ("Willow &amp; Wick Co.") so CI-captured
/// App Store screenshots show real content instead of empty states. Only runs when the
/// SEED_DEMO_DATA environment variable is set — passed as SIMCTL_CHILD_SEED_DEMO_DATA by the
/// iOS screenshot workflow — and only when the database is otherwise empty. A real install never
/// has this environment variable set, so this never touches a user's data.
/// </summary>
internal static class DemoDataSeeder
{
	public static async Task SeedIfRequestedAsync(IDataService dataService)
	{
		if (Environment.GetEnvironmentVariable("SEED_DEMO_DATA") != "1")
			return;

		var existing = await dataService.GetAllAsync<InventoryItem>();
		if (existing.Count > 0)
			return;

		var lavender = new InventoryItem { Name = "Lavender Dream Candle", Category = "Candles", CostPerUnit = 3.25m, SalePrice = 14.99m, QuantityOnHand = 42, MinimumStockLevel = 10 };
		var vanilla = new InventoryItem { Name = "Vanilla Bean Candle", Category = "Candles", CostPerUnit = 3.10m, SalePrice = 13.99m, QuantityOnHand = 8, MinimumStockLevel = 10 };
		var seaSalt = new InventoryItem { Name = "Sea Salt & Sage Candle", Category = "Candles", CostPerUnit = 3.40m, SalePrice = 15.99m, QuantityOnHand = 25, MinimumStockLevel = 10 };
		var wicks = new InventoryItem { Name = "Cotton Wicks (100ct)", Category = "Supplies", CostPerUnit = 0.08m, SalePrice = 0m, QuantityOnHand = 340, MinimumStockLevel = 50 };
		var wax = new InventoryItem { Name = "Soy Wax Flakes", Category = "Raw Materials", CostPerUnit = 4.50m, SalePrice = 0m, QuantityOnHand = 25, MinimumStockLevel = 10, UnitOfMeasure = "lb" };
		var jars = new InventoryItem { Name = "Glass Jars (8oz)", Category = "Supplies", CostPerUnit = 1.20m, SalePrice = 0m, QuantityOnHand = 6, MinimumStockLevel = 20 };

		foreach (var item in new[] { lavender, vanilla, seaSalt, wicks, wax, jars })
			await dataService.SaveAsync(item);

		var now = DateTime.Now;
		var sales = new[]
		{
			MakeSale(lavender, 2, now.AddDays(-1), "Emma R."),
			MakeSale(vanilla, 1, now.AddDays(-2), null),
			MakeSale(seaSalt, 3, now.AddDays(-3), "Noah P."),
			MakeSale(lavender, 1, now.AddDays(-4), null),
			MakeSale(vanilla, 2, now.AddDays(-5), "Ava K."),
		};
		foreach (var sale in sales)
			await dataService.SaveAsync(sale);

		var expenses = new[]
		{
			new Expense { Category = "Supplies", Amount = 85.00m, Description = "Glass jars restock", ExpenseDate = now.AddDays(-6) },
			new Expense { Category = "Shipping", Amount = 32.50m, Description = "USPS labels", ExpenseDate = now.AddDays(-5) },
			new Expense { Category = "Marketing", Amount = 50.00m, Description = "Instagram ads", ExpenseDate = now.AddDays(-3) },
			new Expense { Category = "Rent", Amount = 300.00m, Description = "Studio space (partial)", ExpenseDate = now.AddDays(-10) },
		};
		foreach (var expense in expenses)
			await dataService.SaveAsync(expense);
	}

	private static Sale MakeSale(InventoryItem item, decimal qty, DateTime date, string? customerName) => new()
	{
		InventoryItemId = item.Id,
		ItemName = item.Name,
		Quantity = qty,
		SalePricePerUnit = item.SalePrice,
		CostPerUnit = item.CostPerUnit,
		SaleDate = date,
		CustomerName = customerName,
	};
}
