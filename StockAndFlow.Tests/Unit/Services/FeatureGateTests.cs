using FluentAssertions;
using NSubstitute;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Proves the gates are actually wired into the features, not merely available. An entitlement
/// service nobody calls looks perfect in its own tests and earns nothing.
/// </summary>
public class FeatureGateTests
{
    private readonly InMemoryDataService _data;
    private readonly InventoryService _inventory;
    private readonly SalesService _sales;
    private readonly BomService _bom;

    public FeatureGateTests()
    {
        _data = new InMemoryDataService();
        _data.InitializeAsync().Wait();
        _inventory = new InventoryService(_data);
        _bom = new BomService(_data);
        _sales = new SalesService(_data, _inventory, _bom);
    }

    private sealed class Provider : IEntitlementProvider
    {
        public bool IsPro { get; set; }
        public Task RefreshAsync() => Task.CompletedTask;
        public event EventHandler? StatusChanged;
        private void Unused() => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private (EntitlementService Svc, Provider P) Entitlements()
    {
        var p = new Provider();
        return (new EntitlementService(_data, _inventory, p), p);
    }

    private async Task FillInventoryAsync(int count)
    {
        for (int i = 0; i < count; i++)
            await _inventory.CreateOrUpdateItemAsync(
                new InventoryItem { Name = $"Item {i}", SalePrice = 5m, QuantityOnHand = 10m });
    }

    // ---- Add item gate ----

    [Fact]
    public async Task AddItem_AtTheLimit_ShowsUpgradeAndDoesNotOpenTheEditor()
    {
        await FillInventoryAsync(EntitlementService.FreeInventoryItemLimit);
        var (ent, _) = Entitlements();
        var dialogs = Substitute.For<IDialogService>();
        var editor = Substitute.For<IEditorPresenter>();
        var vm = new InventoryViewModel(_inventory, dialogs, editor, ent);

        vm.AddItemCommand.Execute(null);
        await Task.Delay(150);

        await editor.DidNotReceive().ShowAddInventoryAsync();
        await dialogs.Received().ShowAlertAsync(
            Arg.Is<string>(t => t.Contains("30 items")), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AddItem_UnderTheLimit_OpensTheEditor()
    {
        await FillInventoryAsync(5);
        var (ent, _) = Entitlements();
        var editor = Substitute.For<IEditorPresenter>();
        var vm = new InventoryViewModel(_inventory, Substitute.For<IDialogService>(), editor, ent);

        vm.AddItemCommand.Execute(null);
        await Task.Delay(150);

        await editor.Received().ShowAddInventoryAsync();
    }

    [Fact]
    public async Task AddItem_ForPro_OpensTheEditorEvenWayOverTheLimit()
    {
        await FillInventoryAsync(EntitlementService.FreeInventoryItemLimit + 40);
        var (ent, provider) = Entitlements();
        provider.IsPro = true;
        var editor = Substitute.For<IEditorPresenter>();
        var vm = new InventoryViewModel(_inventory, Substitute.For<IDialogService>(), editor, ent);

        vm.AddItemCommand.Execute(null);
        await Task.Delay(150);

        await editor.Received().ShowAddInventoryAsync();
    }

    [Fact]
    public async Task EditingAnExistingItem_IsNeverGated()
    {
        // Being over the limit must not trap someone out of correcting their own data.
        await FillInventoryAsync(EntitlementService.FreeInventoryItemLimit + 3);
        var (ent, _) = Entitlements();
        var editor = Substitute.For<IEditorPresenter>();
        var vm = new InventoryViewModel(_inventory, Substitute.For<IDialogService>(), editor, ent)
        {
            SelectedItem = (await _inventory.GetAllItemsAsync()).First()
        };

        vm.EditItemCommand.Execute(null);
        await Task.Delay(150);

        await editor.Received().ShowEditInventoryAsync(Arg.Any<InventoryItem>());
    }

    // ---- Import gate ----

    private ExportImportService ImportService(EntitlementService ent) =>
        new(_data, _inventory, _sales, new ExpenseService(_data)) { Entitlements = ent };

    [Fact]
    public async Task Import_IsRefusedOnceTheMonthlyAllowanceIsUsed()
    {
        await FillInventoryAsync(2);
        var (ent, _) = Entitlements();
        var svc = ImportService(ent);
        var path = Path.Combine(Path.GetTempPath(), $"sf-gate-{Guid.NewGuid():N}.xlsx");

        try
        {
            await svc.ExportToExcelAsync(path);

            for (int i = 0; i < EntitlementService.FreeImportsPerMonth; i++)
                (await svc.ImportFromExcelAsync(path)).Success.Should().BeTrue($"import {i + 1} is allowed");

            var blocked = await svc.ImportFromExcelAsync(path);

            blocked.Success.Should().BeFalse();
            blocked.BlockedByEntitlement.Should().NotBeNull("the UI needs to offer an upgrade, not show an error");
            blocked.BlockedByEntitlement!.BlockedBy.Should().Be(ProFeature.UnlimitedImports);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Export_IsNeverRefused_HoweverManyTimes()
    {
        await FillInventoryAsync(2);
        var (ent, _) = Entitlements();
        var svc = ImportService(ent);
        var paths = new List<string>();

        try
        {
            for (int i = 0; i < 10; i++)
            {
                var p = Path.Combine(Path.GetTempPath(), $"sf-export-{Guid.NewGuid():N}.xlsx");
                paths.Add(p);
                var result = await svc.ExportToExcelAsync(p);
                File.Exists(result).Should().BeTrue("a backup you can't take is data held hostage");
            }
        }
        finally
        {
            foreach (var p in paths) if (File.Exists(p)) File.Delete(p);
        }
    }

    [Fact]
    public async Task FailedImport_DoesNotConsumeAnAllowance()
    {
        var (ent, _) = Entitlements();
        var svc = ImportService(ent);
        var garbage = Path.Combine(Path.GetTempPath(), $"sf-bad-{Guid.NewGuid():N}.xlsx");

        try
        {
            await File.WriteAllTextAsync(garbage, "not a spreadsheet");

            (await svc.ImportFromExcelAsync(garbage)).Success.Should().BeFalse();

            (await ent.ImportsRemainingThisMonthAsync()).Should().Be(EntitlementService.FreeImportsPerMonth,
                "a broken file must not cost the user an import");
        }
        finally
        {
            if (File.Exists(garbage)) File.Delete(garbage);
        }
    }

    // ---- Invoice gate ----

    private RecordSaleViewModel SaleVm(EntitlementService ent) =>
        new(_sales, _inventory, new BusinessSettingsService(_data),
            new InvoiceService(new BusinessSettingsService(_data)), ent);

    private async Task ConfigureBusinessAsync()
    {
        var settings = new BusinessSettings { BusinessName = "Soyful serene" };
        await _data.SaveAsync(settings);
    }

    [Fact]
    public async Task Invoice_IsRefusedOnceTheMonthlyAllowanceIsUsed()
    {
        await ConfigureBusinessAsync();
        await FillInventoryAsync(1);
        var (ent, _) = Entitlements();
        var vm = SaleVm(ent);

        var item = (await _inventory.GetAllItemsAsync()).First();
        var transaction = new SaleTransaction
        {
            TransactionId = Guid.NewGuid(),
            SaleDate = DateTime.Now,
            Items = new List<Sale> { new() { ItemName = item.Name, Quantity = 1, SalePricePerUnit = 5m } }
        };

        var produced = new List<string>();
        for (int i = 0; i < EntitlementService.FreeInvoicesPerMonth; i++)
        {
            var p = Path.Combine(Path.GetTempPath(), $"sf-inv-{Guid.NewGuid():N}.pdf");
            produced.Add(p);
            (await vm.GenerateInvoiceAsync(transaction, p)).Should().NotBeNull($"invoice {i + 1} is allowed");
        }

        try
        {
            var blockedPath = Path.Combine(Path.GetTempPath(), $"sf-inv-{Guid.NewGuid():N}.pdf");
            var result = await vm.GenerateInvoiceAsync(transaction, blockedPath);

            result.Should().BeNull();
            vm.LastInvoiceBlock.Should().NotBeNull("the caller must be able to tell a limit from a failure");
            vm.LastInvoiceBlock!.BlockedBy.Should().Be(ProFeature.UnlimitedInvoices);
            File.Exists(blockedPath).Should().BeFalse("no PDF should be written when refused");
        }
        finally
        {
            foreach (var p in produced) if (File.Exists(p)) File.Delete(p);
        }
    }

    [Fact]
    public async Task RecordingASale_StillWorksWhenInvoicesAreExhausted()
    {
        // The sale is the business record; the PDF is a nicety. Losing the sale would be
        // unforgivable, so this is the most important assertion in the file.
        await ConfigureBusinessAsync();
        await FillInventoryAsync(1);
        var (ent, _) = Entitlements();
        for (int i = 0; i < 20; i++) await ent.RecordInvoiceGeneratedAsync();

        var item = (await _inventory.GetAllItemsAsync()).First();
        var sale = await _sales.RecordSaleAsync(item.Id, 2m);

        sale.Should().NotBeNull();
        (await _sales.GetAllSalesAsync()).Should().ContainSingle();
        (await _inventory.GetAllItemsAsync()).Single().QuantityOnHand.Should().Be(8m);
    }

    // ---- Shopify gate ----

    [Fact]
    public async Task ShopifySync_IsRefusedForFree_AndExplainsWhy()
    {
        var (ent, _) = Entitlements();
        var dialogs = Substitute.For<IDialogService>();
        var shopify = new ShopifyService(_data, _inventory, _sales);
        var vm = new ShopifySettingsViewModel(_data, shopify, dialogs, ent);

        vm.SyncNowCommand.Execute(null);
        await Task.Delay(200);

        await dialogs.Received().ShowAlertAsync(
            Arg.Is<string>(t => t.Contains("Pro")), Arg.Any<string>(), Arg.Any<string>());
    }
}
