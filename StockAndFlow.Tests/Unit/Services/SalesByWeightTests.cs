using StockAndFlow.Tests.TestUtilities;
using StockAndFlow.ViewModels;
using StockAndFlow.Platform;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Tests for selling by weight/volume (fractional quantities) and for the
/// sale-edit flow's inventory delta logic.
/// </summary>
public class SalesByWeightTests
{
    private static (InMemoryDataService data, InventoryService inventory, SalesService sales) CreateServices()
    {
        var data = new InMemoryDataService();
        data.InitializeAsync().Wait();
        var inventory = new InventoryService(data);
        var sales = new SalesService(data, inventory, new BomService(data));
        return (data, inventory, sales);
    }

    [Fact]
    public async Task RecordSaleAsync_FractionalQuantity_ReducesStockExactly()
    {
        var (_, inventory, sales) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.30m, salePrice: 0.75m,
            quantity: 90m, unitOfMeasure: "oz");
        await inventory.CreateOrUpdateItemAsync(wax);

        var sale = await sales.RecordSaleAsync(wax.Id, quantity: 12.5m);

        sale.Should().NotBeNull();
        sale!.Quantity.Should().Be(12.5m);
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(77.5m);
    }

    [Fact]
    public async Task RecordSaleAsync_FractionalQuantity_MoneyMathIsExact()
    {
        var (_, inventory, sales) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.30m, salePrice: 0.75m,
            quantity: 90m, unitOfMeasure: "oz");
        await inventory.CreateOrUpdateItemAsync(wax);

        var sale = await sales.RecordSaleAsync(wax.Id, quantity: 12.5m);

        sale!.Subtotal.Should().Be(9.375m);   // 12.5 × $0.75
        sale.COGS.Should().Be(3.75m);         // 12.5 × $0.30
        sale.Profit.Should().Be(5.625m);
    }

    [Fact]
    public async Task RecordSaleAsync_FractionalQuantityExceedsStock_Throws()
    {
        var (_, inventory, sales) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", quantity: 10m, unitOfMeasure: "oz");
        await inventory.CreateOrUpdateItemAsync(wax);

        var action = async () => await sales.RecordSaleAsync(wax.Id, quantity: 10.1m);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient inventory*");
        (await inventory.GetItemByIdAsync(wax.Id))!.QuantityOnHand.Should().Be(10m);
    }

    [Fact]
    public void SaleTransaction_TotalsWithFractionalQuantities_SumExactly()
    {
        var transaction = new SaleTransaction
        {
            TransactionId = Guid.NewGuid(),
            Items = new List<Sale>
            {
                TestDataBuilder.CreateSale(quantity: 2.5m, salePrice: 4m, cost: 1m),
                TestDataBuilder.CreateSale(quantity: 1.25m, salePrice: 8m, cost: 2m),
            }
        };

        transaction.TotalQuantity.Should().Be(3.75m);
        transaction.Revenue.Should().Be(20m);   // 2.5×$4 + 1.25×$8
        transaction.COGS.Should().Be(5m);       // 2.5×$1 + 1.25×$2
        transaction.Profit.Should().Be(15m);
    }

    [Fact]
    public async Task EditSaleViewModel_IncreasingQuantity_DeductsOnlyTheDelta()
    {
        var (data, inventory, sales) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem("Widget", quantity: 100);
        await inventory.CreateOrUpdateItemAsync(item);
        var sale = await sales.RecordSaleAsync(item.Id, quantity: 10); // stock now 90

        var transaction = new SaleTransaction
        {
            TransactionId = sale!.TransactionId,
            SaleDate = sale.SaleDate,
            Items = new List<Sale> { sale }
        };
        var vm = new EditSaleViewModel(sales, inventory, new BomService(data), transaction, Substitute.For<IDialogService>());
        vm.Items[0].Quantity = 15; // sold 5 more than originally recorded

        var closed = await SaveAndWaitForCloseAsync(vm);

        closed.Should().BeTrue("save must complete and close the dialog");
        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(85,
            "only the extra 5 should be deducted, not the whole new quantity");
        (await sales.GetSaleByIdAsync(sale.Id))!.Quantity.Should().Be(15);
    }

    [Fact]
    public async Task EditSaleViewModel_DecreasingQuantity_RestoresTheDelta()
    {
        var (data, inventory, sales) = CreateServices();
        var item = TestDataBuilder.CreateInventoryItem("Widget", quantity: 100);
        await inventory.CreateOrUpdateItemAsync(item);
        var sale = await sales.RecordSaleAsync(item.Id, quantity: 10); // stock now 90

        var transaction = new SaleTransaction
        {
            TransactionId = sale!.TransactionId,
            SaleDate = sale.SaleDate,
            Items = new List<Sale> { sale }
        };
        var vm = new EditSaleViewModel(sales, inventory, new BomService(data), transaction, Substitute.For<IDialogService>());
        vm.Items[0].Quantity = 4; // sold 6 fewer than originally recorded

        var closed = await SaveAndWaitForCloseAsync(vm);

        closed.Should().BeTrue();
        (await inventory.GetItemByIdAsync(item.Id))!.QuantityOnHand.Should().Be(96);
        (await sales.GetSaleByIdAsync(sale.Id))!.Quantity.Should().Be(4);
    }

    /// <summary>
    /// SaveCommand wraps an async method the command interface can't await, so wait for
    /// the CloseRequested event (with a timeout) to know the save finished.
    /// </summary>
    private static async Task<bool> SaveAndWaitForCloseAsync(EditSaleViewModel vm)
    {
        var closed = new TaskCompletionSource<bool>();
        vm.CloseRequested += (_, ok) => closed.TrySetResult(ok);

        vm.SaveCommand.Execute(null);

        var finished = await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        return finished == closed.Task && await closed.Task;
    }
}
