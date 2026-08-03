using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    /// <summary>One purchase option on the paywall.</summary>
    public sealed class ProPlan
    {
        public required string Id { get; init; }
        public required string Name { get; init; }

        /// <summary>Display price. Replaced by the store's localised price once billing is wired.</summary>
        public required string Price { get; init; }

        public string? PricePeriod { get; init; }
        public string? Badge { get; init; }
        public string? Note { get; init; }

        /// <summary>The plan presented as the default choice.</summary>
        public bool IsRecommended { get; init; }
    }

    /// <summary>A line in the Free vs Pro comparison.</summary>
    public sealed class PlanComparisonRow
    {
        public required string Feature { get; init; }
        public required string Free { get; init; }
        public required string Pro { get; init; }
    }

    /// <summary>
    /// The upgrade screen. Prices come from MONETIZATION_PLAN and are placeholders until the
    /// stores supply localised ones; the comparison rows are built from
    /// <see cref="EntitlementService"/> constants so the sales pitch cannot drift from what the
    /// gates actually enforce.
    /// </summary>
    public class PaywallViewModel : ViewModelBase
    {
        private readonly EntitlementService _entitlements;
        private readonly IPurchaseService _purchases;
        private readonly IDialogService _dialogService;

        private ProPlan _selectedPlan;
        private string? _statusMessage;
        private bool _isBusy;

        /// <summary>Raised when the screen should close (purchase succeeded, or user dismissed).</summary>
        public event EventHandler? CloseRequested;

        public PaywallViewModel(
            EntitlementService entitlements,
            IDialogService dialogService,
            IPurchaseService? purchases = null,
            ProFeature? triggeredBy = null)
        {
            _entitlements = entitlements;
            _dialogService = dialogService;
            _purchases = purchases ?? new UnavailablePurchaseService();
            TriggeredBy = triggeredBy;

            Plans = new ObservableCollection<ProPlan>(BuildPlans());
            _selectedPlan = Plans.First(p => p.IsRecommended);
            Comparison = new ObservableCollection<PlanComparisonRow>(BuildComparison());

            PurchaseCommand = new RelayCommand(async () => await PurchaseAsync(), () => !IsBusy);
            RestoreCommand = new RelayCommand(async () => await RestoreAsync(), () => !IsBusy);
            SelectPlanCommand = new RelayCommand<ProPlan>(plan => { if (plan != null) SelectedPlan = plan; });
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>Which gate sent the user here, if any — used to lead with what they were doing.</summary>
        public ProFeature? TriggeredBy { get; }

        public ObservableCollection<ProPlan> Plans { get; }
        public ObservableCollection<PlanComparisonRow> Comparison { get; }

        public bool IsPro => _entitlements.IsPro;

        public string Headline => IsPro ? "You're on Pro" : "Stock & Flow Pro";

        public string SubHeadline => TriggeredBy switch
        {
            ProFeature.UnlimitedInventoryItems => "Room for as many products and supplies as you need.",
            ProFeature.UnlimitedInvoices => "Send as many customer invoices as you like.",
            ProFeature.UnlimitedImports => "Import spreadsheets as often as you need.",
            ProFeature.ShopifySync => "Keep Shopify and your in-person sales in one place.",
            _ => "Everything unlimited, plus Shopify sync."
        };

        /// <summary>Reassurance shown near the buy button — the privacy story is a selling point.</summary>
        public string PrivacyNote =>
            "No account to create. Your inventory stays on this device — we never see your data.";

        public string PurchaseUnavailableNote =>
            "Purchases aren't switched on in this build yet. These are the real plans; the buttons go live once the app is in the stores.";

        public bool IsPurchaseAvailable => _purchases.IsAvailable;

        /// <summary>Bindable inverse, so views don't need a negating converter.</summary>
        public bool ShowPurchaseUnavailableNote => !_purchases.IsAvailable;

        public ProPlan SelectedPlan
        {
            get => _selectedPlan;
            set
            {
                if (SetProperty(ref _selectedPlan, value))
                    OnPropertyChanged(nameof(BuyButtonText));
            }
        }

        public string BuyButtonText => SelectedPlan.Id == "lifetime"
            ? $"Buy Pro for {SelectedPlan.Price}"
            : $"Start Pro — {SelectedPlan.Price}{SelectedPlan.PricePeriod}";

        public string? StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand PurchaseCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand SelectPlanCommand { get; }
        public ICommand CloseCommand { get; }

        private static IEnumerable<ProPlan> BuildPlans() => new[]
        {
            new ProPlan
            {
                Id = "monthly",
                Name = "Monthly",
                Price = "$8.99",
                PricePeriod = "/month",
                Note = "Cancel any time."
            },
            new ProPlan
            {
                Id = "annual",
                Name = "Yearly",
                Price = "$49.99",
                PricePeriod = "/year",
                Badge = "Best value",
                Note = "Works out at $4.17 a month.",
                IsRecommended = true
            },
            new ProPlan
            {
                Id = "lifetime",
                Name = "Lifetime",
                Price = "$99.99",
                Badge = "Founding price",
                Note = "Pay once, keep it forever. This price goes up after launch."
            }
        };

        /// <summary>
        /// Built from the entitlement constants rather than hardcoded, so the comparison can never
        /// promise something different from what the gates enforce.
        /// </summary>
        private static IEnumerable<PlanComparisonRow> BuildComparison() => new[]
        {
            new PlanComparisonRow
            {
                Feature = "Products & supplies",
                Free = $"{EntitlementService.FreeInventoryItemLimit}",
                Pro = "Unlimited"
            },
            new PlanComparisonRow
            {
                Feature = "Recording sales",
                Free = "Unlimited",
                Pro = "Unlimited"
            },
            new PlanComparisonRow
            {
                Feature = "Customer invoices",
                Free = $"{EntitlementService.FreeInvoicesPerMonth} a month",
                Pro = "Unlimited"
            },
            new PlanComparisonRow
            {
                Feature = "Excel backup",
                Free = "Unlimited",
                Pro = "Unlimited"
            },
            new PlanComparisonRow
            {
                Feature = "Excel import",
                Free = $"{EntitlementService.FreeImportsPerMonth} a month",
                Pro = "Unlimited"
            },
            new PlanComparisonRow
            {
                Feature = "Barcode labels",
                Free = "Included",
                Pro = "Included"
            },
            new PlanComparisonRow
            {
                Feature = "Shopify sync",
                Free = "—",
                Pro = "Included"
            }
        };

        private async Task PurchaseAsync()
        {
            if (!_purchases.IsAvailable)
            {
                StatusMessage = PurchaseUnavailableNote;
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;

                var outcome = await _purchases.PurchaseAsync(SelectedPlan.Id);

                if (outcome.Cancelled)
                {
                    StatusMessage = null;   // walking away is not an error
                    return;
                }

                if (!outcome.Success)
                {
                    StatusMessage = outcome.Message ?? "That didn't go through. Nothing has been charged.";
                    return;
                }

                await _entitlements.RefreshAsync();
                await _dialogService.ShowAlertAsync("You're on Pro",
                    "Thank you! Everything's unlocked. Your purchase is tied to your store account, " +
                    "so you can restore it on a new device any time.");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogError(ex, "Purchase failed for plan {Plan}", SelectedPlan.Id);
                StatusMessage = "Something went wrong talking to the store. Nothing has been charged.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RestoreAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = null;

                var outcome = await _purchases.RestoreAsync();
                await _entitlements.RefreshAsync();

                if (_entitlements.IsPro)
                {
                    await _dialogService.ShowAlertAsync("Pro restored", "Your purchase is back. Thanks for sticking with us!");
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                StatusMessage = outcome.Message ??
                    "We couldn't find a purchase on this store account. If you bought Pro with a different account, sign in with that one and try again.";
            }
            catch (Exception ex)
            {
                LogError(ex, "Restore failed");
                StatusMessage = "Something went wrong checking your purchases. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
