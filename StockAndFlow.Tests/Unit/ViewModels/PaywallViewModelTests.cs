using FluentAssertions;
using NSubstitute;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.Tests.TestUtilities;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Tests.Unit.ViewModels;

/// <summary>
/// The upgrade screen. Two things must hold: the pitch can never promise more than the gates
/// enforce, and a purchase that doesn't complete must never leave the user thinking they paid.
/// </summary>
public class PaywallViewModelTests
{
    private readonly InMemoryDataService _data;
    private readonly InventoryService _inventory;

    public PaywallViewModelTests()
    {
        _data = new InMemoryDataService();
        _data.InitializeAsync().Wait();
        _inventory = new InventoryService(_data);
    }

    private sealed class Provider : IEntitlementProvider
    {
        public bool IsPro { get; set; }
        public int RefreshCount { get; private set; }
        public Task RefreshAsync() { RefreshCount++; return Task.CompletedTask; }
        public event EventHandler? StatusChanged;
        public void Notify() => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakePurchases : IPurchaseService
    {
        public bool IsAvailable { get; set; } = true;
        public PurchaseOutcome NextOutcome { get; set; } = new(true);
        public string? LastPlanId { get; private set; }
        public bool RestoreCalled { get; private set; }
        public Action? OnPurchase { get; set; }

        public Task<PurchaseOutcome> PurchaseAsync(string planId)
        {
            LastPlanId = planId;
            OnPurchase?.Invoke();
            return Task.FromResult(NextOutcome);
        }

        public Task<PurchaseOutcome> RestoreAsync()
        {
            RestoreCalled = true;
            return Task.FromResult(NextOutcome);
        }
    }

    private (PaywallViewModel Vm, Provider P, FakePurchases Purchases, IDialogService Dialogs) Create(
        ProFeature? triggeredBy = null)
    {
        var provider = new Provider();
        var entitlements = new EntitlementService(_data, _inventory, provider);
        var purchases = new FakePurchases();
        var dialogs = Substitute.For<IDialogService>();
        return (new PaywallViewModel(entitlements, dialogs, purchases, triggeredBy), provider, purchases, dialogs);
    }

    // ---- Presentation ----

    [Fact]
    public void OffersThreePlans_WithYearlyRecommended()
    {
        var (vm, _, _, _) = Create();

        vm.Plans.Select(p => p.Id).Should().BeEquivalentTo(new[] { "monthly", "annual", "lifetime" });
        vm.Plans.Should().ContainSingle(p => p.IsRecommended)
            .Which.Id.Should().Be("annual", "the yearly plan is the hero price");
        vm.SelectedPlan.Id.Should().Be("annual", "the recommended plan is preselected");
    }

    [Fact]
    public void ComparisonIsBuiltFromTheRealLimits_NotHardcodedCopy()
    {
        // If someone changes a limit, the sales pitch must change with it.
        var (vm, _, _, _) = Create();

        vm.Comparison.Single(r => r.Feature.Contains("Products")).Free
            .Should().Be(EntitlementService.FreeInventoryItemLimit.ToString());
        vm.Comparison.Single(r => r.Feature.Contains("invoices")).Free
            .Should().Contain(EntitlementService.FreeInvoicesPerMonth.ToString());
        vm.Comparison.Single(r => r.Feature.Contains("import")).Free
            .Should().Contain(EntitlementService.FreeImportsPerMonth.ToString());
    }

    [Fact]
    public void ComparisonShowsSalesAndExportUnlimitedOnBothPlans()
    {
        // These are promises the free tier makes; the paywall must not imply otherwise.
        var (vm, _, _, _) = Create();

        var sales = vm.Comparison.Single(r => r.Feature.Contains("Recording sales"));
        sales.Free.Should().Be("Unlimited");
        sales.Pro.Should().Be("Unlimited");

        var backup = vm.Comparison.Single(r => r.Feature.Contains("backup"));
        backup.Free.Should().Be("Unlimited");
    }

    [Theory]
    [InlineData(ProFeature.UnlimitedInventoryItems, "products")]
    [InlineData(ProFeature.UnlimitedInvoices, "invoices")]
    [InlineData(ProFeature.UnlimitedImports, "spreadsheets")]
    [InlineData(ProFeature.ShopifySync, "Shopify")]
    public void LeadsWithWhateverTheUserWasTryingToDo(ProFeature feature, string expected)
    {
        var (vm, _, _, _) = Create(feature);

        vm.SubHeadline.Should().ContainEquivalentOf(expected);
    }

    [Fact]
    public void BuyButtonWordsLifetimeDifferentlyFromSubscriptions()
    {
        var (vm, _, _, _) = Create();

        vm.SelectedPlan = vm.Plans.Single(p => p.Id == "annual");
        vm.BuyButtonText.Should().Be("Start Pro — $49.99/year");

        vm.SelectedPlan = vm.Plans.Single(p => p.Id == "lifetime");
        vm.BuyButtonText.Should().Be("Buy Pro for $99.99", "'start' implies a subscription");
    }

    [Fact]
    public void SelectPlanCommand_ChangesTheSelection()
    {
        var (vm, _, _, _) = Create();

        vm.SelectPlanCommand.Execute(vm.Plans.Single(p => p.Id == "monthly"));

        vm.SelectedPlan.Id.Should().Be("monthly");
    }

    [Fact]
    public void HeadlineReflectsExistingProStatus()
    {
        var (vm, provider, _, _) = Create();
        vm.Headline.Should().Be("Stock & Flow Pro");

        provider.IsPro = true;
        var (proVm, proProvider, _, _) = Create();
        proProvider.IsPro = true;
        proVm.IsPro.Should().BeTrue();
        proVm.Headline.Should().Be("You're on Pro");
    }

    // ---- Purchase flow ----

    [Fact]
    public async Task SuccessfulPurchase_RefreshesEntitlementAndCloses()
    {
        var (vm, provider, purchases, dialogs) = Create();
        // The store grants Pro; the provider learns about it on refresh.
        purchases.OnPurchase = () => provider.IsPro = true;
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.PurchaseCommand.Execute(null);
        await Task.Delay(150);

        purchases.LastPlanId.Should().Be("annual");
        provider.RefreshCount.Should().BeGreaterThan(0, "entitlement must be re-read, not assumed");
        await dialogs.Received().ShowAlertAsync(
            Arg.Is<string>(t => t.Contains("Pro")), Arg.Any<string>(), Arg.Any<string>());
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task CancelledPurchase_SaysNothingAndStaysOpen()
    {
        var (vm, _, purchases, dialogs) = Create();
        purchases.NextOutcome = PurchaseOutcome.Cancel;
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.PurchaseCommand.Execute(null);
        await Task.Delay(150);

        vm.StatusMessage.Should().BeNull("changing your mind isn't an error worth a message");
        closed.Should().BeFalse();
        await dialogs.DidNotReceive().ShowAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FailedPurchase_ReassuresNothingWasCharged_AndDoesNotGrantPro()
    {
        var (vm, provider, purchases, _) = Create();
        purchases.NextOutcome = new PurchaseOutcome(false, null);

        vm.PurchaseCommand.Execute(null);
        await Task.Delay(150);

        vm.StatusMessage.Should().Contain("charged", "money anxiety needs addressing explicitly");
        provider.IsPro.Should().BeFalse();
    }

    [Fact]
    public async Task WhenBillingIsUnavailable_ExplainsInsteadOfFailingSilently()
    {
        var (vm, _, purchases, _) = Create();
        purchases.IsAvailable = false;

        vm.IsPurchaseAvailable.Should().BeFalse();
        vm.PurchaseCommand.Execute(null);
        await Task.Delay(150);

        vm.StatusMessage.Should().Contain("aren't switched on");
        purchases.LastPlanId.Should().BeNull("no attempt should be made");
    }

    // ---- Restore ----

    [Fact]
    public async Task Restore_WhenAPurchaseExists_UnlocksAndCloses()
    {
        var (vm, provider, purchases, dialogs) = Create();
        purchases.NextOutcome = new PurchaseOutcome(true);
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        // Restoring finds the purchase, so the provider now reports Pro.
        provider.IsPro = true;

        vm.RestoreCommand.Execute(null);
        await Task.Delay(150);

        purchases.RestoreCalled.Should().BeTrue();
        closed.Should().BeTrue();
        await dialogs.Received().ShowAlertAsync(
            Arg.Is<string>(t => t.Contains("restored")), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Restore_WithNothingToRestore_SuggestsCheckingTheStoreAccount()
    {
        var (vm, _, purchases, _) = Create();
        purchases.NextOutcome = new PurchaseOutcome(false, null);

        vm.RestoreCommand.Execute(null);
        await Task.Delay(150);

        vm.StatusMessage.Should().Contain("different account",
            "the usual cause is being signed in with the wrong store account");
    }

    [Fact]
    public void PrivacyNoteIsPartOfThePitch()
    {
        var (vm, _, _, _) = Create();

        vm.PrivacyNote.Should().ContainEquivalentOf("no account");
        vm.PrivacyNote.Should().ContainEquivalentOf("device");
    }
}
