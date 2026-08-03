using FluentAssertions;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// The free/Pro boundary. Two failure directions matter and both are bad: blocking a paying
/// customer, and giving away a paid feature. The rules that must never regress are that
/// recording a sale and exporting a backup are never gated.
/// </summary>
public class EntitlementServiceTests
{
    private readonly InMemoryDataService _data;
    private readonly InventoryService _inventory;

    public EntitlementServiceTests()
    {
        _data = new InMemoryDataService();
        _data.InitializeAsync().Wait();
        _inventory = new InventoryService(_data);
    }

    private sealed class FakeProvider : IEntitlementProvider
    {
        private bool _isPro;
        public bool IsPro => _isPro;
        public int RefreshCount { get; private set; }
        public Task RefreshAsync() { RefreshCount++; return Task.CompletedTask; }
        public event EventHandler? StatusChanged;
        public void SetPro(bool value)
        {
            _isPro = value;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private EntitlementService Free() => new(_data, _inventory, new FakeProvider());
    private (EntitlementService Svc, FakeProvider Provider) WithProvider()
    {
        var p = new FakeProvider();
        return (new EntitlementService(_data, _inventory, p), p);
    }

    private async Task AddItemsAsync(int count)
    {
        for (int i = 0; i < count; i++)
            await _inventory.CreateOrUpdateItemAsync(
                new InventoryItem { Name = $"Item {i}", SalePrice = 1m, QuantityOnHand = 10m });
    }

    // ---- Defaults ----

    [Fact]
    public void WithNoProvider_DefaultsToFree_NotPro()
    {
        // A missing provider must under-report, never hand out Pro by accident.
        new EntitlementService(_data, _inventory).IsPro.Should().BeFalse();
    }

    // ---- Inventory item limit ----

    [Fact]
    public async Task Free_AllowsItemsUpToTheLimit()
    {
        await AddItemsAsync(EntitlementService.FreeInventoryItemLimit - 1);

        (await Free().CanAddInventoryItemAsync()).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Free_BlocksTheItemThatWouldExceedTheLimit()
    {
        await AddItemsAsync(EntitlementService.FreeInventoryItemLimit);

        var result = await Free().CanAddInventoryItemAsync();

        result.Allowed.Should().BeFalse();
        result.BlockedBy.Should().Be(ProFeature.UnlimitedInventoryItems);
        result.Title.Should().NotBeNullOrWhiteSpace();
        result.Message.Should().Contain("record sales", "the message must say what still works");
    }

    [Fact]
    public async Task Pro_HasNoItemLimit()
    {
        await AddItemsAsync(EntitlementService.FreeInventoryItemLimit + 25);
        var (svc, provider) = WithProvider();
        provider.SetPro(true);

        (await svc.CanAddInventoryItemAsync()).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task ItemSlotsRemaining_CountsDown_AndIsNullForPro()
    {
        await AddItemsAsync(28);
        var (svc, provider) = WithProvider();

        (await svc.InventorySlotsRemainingAsync()).Should().Be(2);

        provider.SetPro(true);
        (await svc.InventorySlotsRemainingAsync()).Should().BeNull("Pro has no limit to report");
    }

    [Fact]
    public async Task ItemSlotsRemaining_NeverGoesNegative()
    {
        await AddItemsAsync(EntitlementService.FreeInventoryItemLimit + 5);

        (await Free().InventorySlotsRemainingAsync()).Should().Be(0);
    }

    // ---- Invoices ----

    [Fact]
    public async Task Free_AllowsFiveInvoicesThenBlocks()
    {
        var svc = Free();

        for (int i = 1; i <= EntitlementService.FreeInvoicesPerMonth; i++)
        {
            (await svc.CanGenerateInvoiceAsync()).Allowed.Should().BeTrue($"invoice {i} is within the allowance");
            await svc.RecordInvoiceGeneratedAsync();
        }

        var blocked = await svc.CanGenerateInvoiceAsync();
        blocked.Allowed.Should().BeFalse();
        blocked.BlockedBy.Should().Be(ProFeature.UnlimitedInvoices);
        blocked.Message.Should().Contain("sale is still recorded",
            "the user must know the sale isn't lost, only the PDF");
    }

    [Fact]
    public async Task InvoicesRemaining_CountsDown()
    {
        var svc = Free();
        (await svc.InvoicesRemainingThisMonthAsync()).Should().Be(5);

        await svc.RecordInvoiceGeneratedAsync();
        await svc.RecordInvoiceGeneratedAsync();

        (await svc.InvoicesRemainingThisMonthAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Pro_HasUnlimitedInvoices()
    {
        var (svc, provider) = WithProvider();
        provider.SetPro(true);

        for (int i = 0; i < 20; i++)
            await svc.RecordInvoiceGeneratedAsync();

        (await svc.CanGenerateInvoiceAsync()).Allowed.Should().BeTrue();
        (await svc.InvoicesRemainingThisMonthAsync()).Should().BeNull();
    }

    // ---- Imports ----

    [Fact]
    public async Task Free_AllowsTwoImportsThenBlocks()
    {
        var svc = Free();

        (await svc.CanImportAsync()).Allowed.Should().BeTrue();
        await svc.RecordImportAsync();
        (await svc.CanImportAsync()).Allowed.Should().BeTrue();
        await svc.RecordImportAsync();

        var blocked = await svc.CanImportAsync();
        blocked.Allowed.Should().BeFalse();
        blocked.BlockedBy.Should().Be(ProFeature.UnlimitedImports);
        blocked.Message.Should().Contain("Exporting", "the message must reassure that backups stay free");
    }

    // ---- Shopify ----

    [Fact]
    public void Free_CannotUseShopifySync()
    {
        var result = Free().CanUseShopifySync();

        result.Allowed.Should().BeFalse();
        result.BlockedBy.Should().Be(ProFeature.ShopifySync);
    }

    [Fact]
    public void Pro_CanUseShopifySync()
    {
        var (svc, provider) = WithProvider();
        provider.SetPro(true);

        svc.CanUseShopifySync().Allowed.Should().BeTrue();
    }

    // ---- Monthly reset ----

    [Fact]
    public async Task CountersResetWhenTheMonthChanges()
    {
        var svc = Free();
        await svc.RecordInvoiceGeneratedAsync();
        await svc.RecordInvoiceGeneratedAsync();
        await svc.RecordImportAsync();
        await svc.RecordImportAsync();

        (await svc.CanGenerateInvoiceAsync()).Allowed.Should().BeTrue();
        (await svc.CanImportAsync()).Allowed.Should().BeFalse();

        // Simulate the calendar rolling over by ageing the stored period.
        var settings = await _data.GetSettingsAsync();
        settings.UsagePeriod = "2020-01";
        await _data.SaveSettingsAsync(settings);

        (await svc.InvoicesRemainingThisMonthAsync()).Should().Be(5, "a new month starts fresh");
        (await svc.CanImportAsync()).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task PeriodRollover_IsPersisted_NotJustComputed()
    {
        var svc = Free();
        var settings = await _data.GetSettingsAsync();
        settings.UsagePeriod = "2020-01";
        settings.InvoicesThisPeriod = 5;
        await _data.SaveSettingsAsync(settings);

        await svc.CanGenerateInvoiceAsync();

        var reloaded = await _data.GetSettingsAsync();
        reloaded.UsagePeriod.Should().Be(DateTime.Now.ToString("yyyy-MM"));
        reloaded.InvoicesThisPeriod.Should().Be(0,
            "the reset must be written so it survives without another settings save");
    }

    // ---- Upgrade takes effect immediately ----

    [Fact]
    public async Task UpgradingMidSession_LiftsGatesWithoutRestart()
    {
        await AddItemsAsync(EntitlementService.FreeInventoryItemLimit);
        var (svc, provider) = WithProvider();
        (await svc.CanAddInventoryItemAsync()).Allowed.Should().BeFalse();

        var notified = 0;
        svc.EntitlementChanged += (_, _) => notified++;
        provider.SetPro(true);

        (await svc.CanAddInventoryItemAsync()).Allowed.Should().BeTrue(
            "someone who just paid must not have to restart the app");
        notified.Should().Be(1, "open screens need to hear about it to drop their gates");
    }

    [Fact]
    public async Task Refresh_DelegatesToTheProvider()
    {
        var (svc, provider) = WithProvider();

        await svc.RefreshAsync();

        provider.RefreshCount.Should().Be(1);
    }

    // ---- Things that must never be gated ----

    [Fact]
    public async Task RecordingASale_IsNeverGated_EvenAtEveryLimit()
    {
        // The heartbeat rule: a free user standing at a market stall must always be able to
        // record what they just sold. There is deliberately no CanRecordSale gate to call.
        await AddItemsAsync(EntitlementService.FreeInventoryItemLimit + 10);
        var svc = Free();
        for (int i = 0; i < 10; i++)
        {
            await svc.RecordInvoiceGeneratedAsync();
            await svc.RecordImportAsync();
        }

        var item = (await _inventory.GetAllItemsAsync()).First();
        var sales = new SalesService(_data, _inventory, new BomService(_data));

        var sale = await sales.RecordSaleAsync(item.Id, 1m);

        sale.Should().NotBeNull("recording a sale must never be blocked by entitlement");
        typeof(EntitlementService).GetMethods()
            .Should().NotContain(m => m.Name.Contains("Sale"),
                "no sale gate should exist to be called by mistake");
    }

    [Fact]
    public void ExcelExport_HasNoGate()
    {
        // "Your data is never hostage" — a backup you can't take is exactly that.
        typeof(EntitlementService).GetMethods()
            .Should().NotContain(m => m.Name.Contains("Export"),
                "export must have no gate to call");
    }
}
