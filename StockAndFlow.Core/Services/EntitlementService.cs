using System;
using System.Threading.Tasks;
using Serilog;
using StockAndFlow.Platform;

namespace StockAndFlow.Services
{
    /// <summary>A gated capability.</summary>
    public enum ProFeature
    {
        UnlimitedInventoryItems,
        UnlimitedInvoices,
        UnlimitedImports,
        ShopifySync
    }

    /// <summary>
    /// Answer to "may I do this?". Carries the wording to show the user, so every gate in the
    /// app speaks with one voice instead of each screen inventing its own upsell.
    /// </summary>
    public sealed record EntitlementResult(bool Allowed, ProFeature? BlockedBy = null, string? Title = null, string? Message = null)
    {
        public static EntitlementResult Yes { get; } = new(true);
    }

    /// <summary>
    /// The single place that decides what the free tier allows. ViewModels ask this; nothing
    /// else knows the limits.
    ///
    /// Design rules, from MONETIZATION_PLAN.md:
    ///  • Recording a sale is NEVER gated — it is the app's heartbeat, and blocking it mid-market
    ///    would make the free tier a trial in disguise.
    ///  • Excel EXPORT is NEVER gated — a backup you can't take is data held hostage.
    ///  • Capped features reset monthly, so the free tier stays usable forever.
    /// </summary>
    public class EntitlementService
    {
        /// <summary>Free-tier ceiling on inventory items. Counts supplies too — they are items.</summary>
        public const int FreeInventoryItemLimit = 30;

        /// <summary>Free-tier invoices per calendar month.</summary>
        public const int FreeInvoicesPerMonth = 5;

        /// <summary>Free-tier Excel imports per calendar month.</summary>
        public const int FreeImportsPerMonth = 2;

        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;
        private readonly IEntitlementProvider _provider;

        public EntitlementService(
            IDataService dataService,
            InventoryService inventoryService,
            IEntitlementProvider? provider = null)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
            _provider = provider ?? new FreeEntitlementProvider();
        }

        public bool IsPro => _provider.IsPro;

        /// <summary>Fires when Pro status changes so open screens can drop their gates.</summary>
        public event EventHandler? EntitlementChanged
        {
            add => _provider.StatusChanged += value;
            remove => _provider.StatusChanged -= value;
        }

        public Task RefreshAsync() => _provider.RefreshAsync();

        // ---- Gates ----

        /// <summary>
        /// Whether another inventory item may be created. Editing an existing item is always
        /// allowed, even over the limit — going over must never trap someone out of fixing
        /// their own data.
        /// </summary>
        public async Task<EntitlementResult> CanAddInventoryItemAsync()
        {
            if (IsPro) return EntitlementResult.Yes;

            var count = (await _inventoryService.GetAllItemsAsync()).Count;
            if (count < FreeInventoryItemLimit) return EntitlementResult.Yes;

            return new EntitlementResult(false, ProFeature.UnlimitedInventoryItems,
                "You've reached 30 items",
                $"The free plan holds {FreeInventoryItemLimit} inventory items and you have {count}. " +
                "Pro removes the limit.\n\n" +
                "You can still record sales, edit what you have, and back up to Excel.");
        }

        /// <summary>Whether another invoice may be generated this month.</summary>
        public async Task<EntitlementResult> CanGenerateInvoiceAsync()
        {
            if (IsPro) return EntitlementResult.Yes;

            var settings = await GetSettingsForCurrentPeriodAsync();
            if (settings.InvoicesThisPeriod < FreeInvoicesPerMonth) return EntitlementResult.Yes;

            return new EntitlementResult(false, ProFeature.UnlimitedInvoices,
                "That's 5 invoices this month",
                $"The free plan includes {FreeInvoicesPerMonth} customer invoices a month, and the " +
                "count resets on the 1st. Pro makes them unlimited.\n\n" +
                "The sale is still recorded either way — only the PDF is limited.");
        }

        /// <summary>Whether another Excel import may run this month.</summary>
        public async Task<EntitlementResult> CanImportAsync()
        {
            if (IsPro) return EntitlementResult.Yes;

            var settings = await GetSettingsForCurrentPeriodAsync();
            if (settings.ImportsThisPeriod < FreeImportsPerMonth) return EntitlementResult.Yes;

            return new EntitlementResult(false, ProFeature.UnlimitedImports,
                "That's 2 imports this month",
                $"The free plan includes {FreeImportsPerMonth} Excel imports a month, and the count " +
                "resets on the 1st. Pro makes them unlimited.\n\n" +
                "Exporting a backup is always unlimited — your data is never locked in.");
        }

        /// <summary>Whether the Shopify integration may be used at all.</summary>
        public EntitlementResult CanUseShopifySync()
        {
            if (IsPro) return EntitlementResult.Yes;

            return new EntitlementResult(false, ProFeature.ShopifySync,
                "Shopify sync is a Pro feature",
                "Pro pulls your Shopify products and orders straight into Stock & Flow, and can " +
                "push your stock counts back so your storefront doesn't oversell what you sold " +
                "in person.");
        }

        /// <summary>
        /// Standard handling for a refused action: explain it, offer the upgrade screen, and open
        /// it if they say yes. Keeps every gate behaving identically instead of each screen
        /// inventing its own flow.
        /// </summary>
        /// <returns>True if the user is now Pro and the caller may proceed.</returns>
        public async Task<bool> OfferUpgradeAsync(
            EntitlementResult refusal,
            IDialogService dialogs,
            IPaywallPresenter? paywall)
        {
            if (refusal.Allowed) return true;

            // With nowhere to send them, still say why the action didn't happen.
            if (paywall == null)
            {
                await dialogs.ShowAlertAsync(refusal.Title ?? "Upgrade needed", refusal.Message ?? string.Empty);
                return false;
            }

            var wantsToSee = await dialogs.ShowConfirmAsync(
                refusal.Title ?? "Upgrade needed",
                refusal.Message ?? string.Empty,
                "See Pro",
                "Not now");

            if (!wantsToSee) return false;

            await paywall.ShowPaywallAsync(refusal.BlockedBy);

            // They may have bought while the screen was open.
            return IsPro;
        }

        // ---- Usage recording ----

        /// <summary>
        /// Counts an invoice against this month. Called after one is successfully produced, so a
        /// failed render doesn't burn an allowance.
        /// </summary>
        public Task RecordInvoiceGeneratedAsync() => IncrementAsync(invoices: 1);

        /// <summary>Counts a completed import against this month.</summary>
        public Task RecordImportAsync() => IncrementAsync(imports: 1);

        /// <summary>Remaining free invoices this month; null when Pro (no limit to report).</summary>
        public async Task<int?> InvoicesRemainingThisMonthAsync()
        {
            if (IsPro) return null;
            var settings = await GetSettingsForCurrentPeriodAsync();
            return Math.Max(0, FreeInvoicesPerMonth - settings.InvoicesThisPeriod);
        }

        /// <summary>Remaining free imports this month; null when Pro.</summary>
        public async Task<int?> ImportsRemainingThisMonthAsync()
        {
            if (IsPro) return null;
            var settings = await GetSettingsForCurrentPeriodAsync();
            return Math.Max(0, FreeImportsPerMonth - settings.ImportsThisPeriod);
        }

        /// <summary>Remaining free item slots; null when Pro.</summary>
        public async Task<int?> InventorySlotsRemainingAsync()
        {
            if (IsPro) return null;
            var count = (await _inventoryService.GetAllItemsAsync()).Count;
            return Math.Max(0, FreeInventoryItemLimit - count);
        }

        // ---- Period handling ----

        private static string CurrentPeriod => DateTime.Now.ToString("yyyy-MM");

        /// <summary>
        /// Loads settings, rolling the counters over if the calendar month has changed. The roll
        /// is persisted so the reset survives even if nothing else writes settings this month.
        /// </summary>
        private async Task<Models.AppSettings> GetSettingsForCurrentPeriodAsync()
        {
            var settings = await _dataService.GetSettingsAsync();

            if (settings.UsagePeriod != CurrentPeriod)
            {
                Log.Information("Free-tier usage period rolled over: {Old} -> {New}",
                    settings.UsagePeriod ?? "(none)", CurrentPeriod);

                settings.UsagePeriod = CurrentPeriod;
                settings.InvoicesThisPeriod = 0;
                settings.ImportsThisPeriod = 0;
                await _dataService.SaveSettingsAsync(settings);
            }

            return settings;
        }

        private async Task IncrementAsync(int invoices = 0, int imports = 0)
        {
            try
            {
                var settings = await GetSettingsForCurrentPeriodAsync();
                settings.InvoicesThisPeriod += invoices;
                settings.ImportsThisPeriod += imports;
                await _dataService.SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                // Never fail the user's actual work because bookkeeping couldn't be written.
                Log.Warning(ex, "Could not record free-tier usage");
            }
        }
    }
}
