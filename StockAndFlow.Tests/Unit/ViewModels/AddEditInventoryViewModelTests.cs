using StockAndFlow.Platform;
using StockAndFlow.Tests.TestUtilities;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Tests.Unit.ViewModels;

/// <summary>
/// Tests for the Add/Edit Inventory view model, focused on "Your cost" auto-calculating
/// from the Bill of Materials (component cost × amount used per finished item).
/// </summary>
public class AddEditInventoryViewModelTests
{
    private static (InMemoryDataService data, InventoryService inventory, BomService bom) CreateServices()
    {
        var data = new InMemoryDataService();
        data.InitializeAsync().Wait();
        return (data, new InventoryService(data), new BomService(data));
    }

    private static AddEditInventoryViewModel CreateAddVm(InventoryService inventory, BomService bom) =>
        new(inventory, bom, Substitute.For<IFilePickerService>(), Substitute.For<IDialogService>());

    private void AddComponent(AddEditInventoryViewModel vm, InventoryItem component, decimal qty)
    {
        vm.PendingComponent = component;
        vm.PendingQty = qty;
        vm.AddBomComponentCommand.Execute(null);
    }

    [Fact]
    public async Task Save_WarnsWhenAnotherItemAlreadyUsesTheSameBarcode()
    {
        // Real data hit this: two candles shared SKU "13000", so scanning it silently
        // selected whichever matched first.
        var (data, inventory, bom) = CreateServices();
        await inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Blue Candle", Sku = "13000", SalePrice = 20m, QuantityOnHand = 5 });

        var dialogs = Substitute.For<IDialogService>();
        dialogs.ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
               .Returns(true); // user chooses "Let me change it"

        var vm = new AddEditInventoryViewModel(inventory, bom, Substitute.For<IFilePickerService>(), dialogs);
        await vm.InitializeAsync();
        vm.Name = "Green Candle";
        vm.Sku = "13000";
        vm.SalePrice = 20m;

        vm.SaveCommand.Execute(null);
        await Task.Delay(150);

        await dialogs.Received().ShowConfirmAsync(
            Arg.Is<string>(t => t.Contains("Code already used")),
            Arg.Is<string>(m => m.Contains("Blue Candle")),
            Arg.Any<string>(), Arg.Any<string>());
        (await inventory.GetAllItemsAsync()).Should().HaveCount(1,
            "choosing to change the code must abandon the save");
    }

    [Fact]
    public async Task Save_AllowsReusingItsOwnBarcodeWhenEditing()
    {
        var (data, inventory, bom) = CreateServices();
        var item = await inventory.CreateOrUpdateItemAsync(
            new InventoryItem { Name = "Blue Candle", Sku = "13000", SalePrice = 20m, QuantityOnHand = 5 });

        var dialogs = Substitute.For<IDialogService>();
        var vm = new AddEditInventoryViewModel(inventory, bom, item,
            Substitute.For<IFilePickerService>(), dialogs);
        await vm.InitializeAsync();
        vm.SalePrice = 25m;

        vm.SaveCommand.Execute(null);
        await Task.Delay(150);

        await dialogs.DidNotReceive().ShowConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        (await inventory.GetAllItemsAsync()).Single().SalePrice.Should().Be(25m);
    }

    [Fact]
    public void AddingBomComponents_AutoFillsYourCostFromMaterials()
    {
        var (_, inventory, bom) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.30m, quantity: 90m, unitOfMeasure: "oz");
        var wick = TestDataBuilder.CreateInventoryItem("Wick", cost: 0.15m, quantity: 50);
        var vm = CreateAddVm(inventory, bom);

        AddComponent(vm, wax, 2.5m);   // $0.75 of wax
        AddComponent(vm, wick, 1m);    // $0.15 wick

        vm.HasBom.Should().BeTrue();
        vm.MaterialsCost.Should().Be(0.90m);
        vm.CostPerUnit.Should().Be(0.90m, "Your cost must auto-fill from the materials");
    }

    [Fact]
    public void RemovingAComponent_RecalculatesYourCost()
    {
        var (_, inventory, bom) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.30m, quantity: 90m, unitOfMeasure: "oz");
        var wick = TestDataBuilder.CreateInventoryItem("Wick", cost: 0.15m, quantity: 50);
        var vm = CreateAddVm(inventory, bom);
        AddComponent(vm, wax, 2.5m);
        AddComponent(vm, wick, 1m);

        vm.BomComponents.First(e => e.Item.Id == wick.Id).RemoveCommand.Execute(null);

        vm.MaterialsCost.Should().Be(0.75m);
        vm.CostPerUnit.Should().Be(0.75m);
    }

    [Fact]
    public void ExtraCost_AddsOnTopOfMaterials_AndSurvivesBomChanges()
    {
        var (_, inventory, bom) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.30m, quantity: 90m, unitOfMeasure: "oz");
        var vm = CreateAddVm(inventory, bom);
        AddComponent(vm, wax, 2.5m); // materials 0.75

        vm.ExtraCost = 0.35m; // labor

        vm.CostPerUnit.Should().Be(1.10m, "Your cost = materials + extras");

        var wick = TestDataBuilder.CreateInventoryItem("Wick", cost: 0.15m, quantity: 50);
        AddComponent(vm, wick, 1m); // BOM changed → materials now 0.90

        vm.ExtraCost.Should().Be(0.35m, "the user's extras must never be touched by BOM changes");
        vm.CostPerUnit.Should().Be(1.25m, "total recalculates but keeps the extras on top");
    }

    [Fact]
    public void NoBom_CostStaysWhateverTheUserTyped()
    {
        var (_, inventory, bom) = CreateServices();
        var vm = CreateAddVm(inventory, bom);

        vm.CostPerUnit = 4m;

        vm.HasBom.Should().BeFalse();
        vm.CostPerUnit.Should().Be(4m);
    }

    [Fact]
    public async Task EditMode_LoadingExistingBom_RefreshesMaterialsButKeepsUsersExtras()
    {
        // The candle was saved when wax cost $0.30/oz (materials 0.75 + labor 0.35 = 1.10);
        // wax now costs $0.40/oz. Opening the editor must refresh the materials part
        // without touching the labor the user entered.
        var (_, inventory, bom) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.40m, quantity: 90m, unitOfMeasure: "oz");
        var candle = TestDataBuilder.CreateInventoryItem("Candle", cost: 1.10m, quantity: 20);
        candle.ExtraCostPerUnit = 0.35m;
        await inventory.CreateOrUpdateItemAsync(wax);
        await inventory.CreateOrUpdateItemAsync(candle);
        await bom.SaveComponentsForItemAsync(candle.Id, new[]
        {
            new BomComponent { ParentItemId = candle.Id, ComponentItemId = wax.Id, QuantityPerUnit = 2.5m },
        });

        var vm = new AddEditInventoryViewModel(inventory, bom, candle,
            Substitute.For<IFilePickerService>(), Substitute.For<IDialogService>());
        await vm.InitializeAsync();

        vm.MaterialsCost.Should().Be(1.00m); // 2.5 oz × $0.40
        vm.ExtraCost.Should().Be(0.35m, "the user's labor number must survive the reload");
        vm.CostPerUnit.Should().Be(1.35m, "fresh materials price + untouched extras");
    }

    [Fact]
    public void ComponentRow_ShowsUnitAndLineCost()
    {
        var (_, inventory, bom) = CreateServices();
        var wax = TestDataBuilder.CreateInventoryItem("Wax", cost: 0.30m, quantity: 90m, unitOfMeasure: "oz");
        var vm = CreateAddVm(inventory, bom);

        AddComponent(vm, wax, 2.5m);

        var entry = vm.BomComponents.Single();
        entry.QuantityDisplay.Should().Be($"× {2.5m:0.###} oz");
        entry.LineCost.Should().Be(0.75m);
    }
}
