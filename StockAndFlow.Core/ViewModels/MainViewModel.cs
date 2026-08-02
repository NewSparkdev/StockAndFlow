using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CalculationService _calculationService;
        private readonly ShopifyService _shopifyService;
        private readonly IDialogService _dialogService;
        private readonly BusinessSettingsService _settingsService;

        private BusinessMetrics? _currentMetrics;
        private string _statusMessage = "Ready";
        private bool _isLoading;
        private string? _logoPath;
        private DateTime _startDate;
        private DateTime _endDate;

        public string AppName => "Stock & Flow";

        public BusinessMetrics? CurrentMetrics
        {
            get => _currentMetrics;
            set => SetProperty(ref _currentMetrics, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>Stored path of the business logo (shown on the dashboard). Empty if none set.</summary>
        public string? LogoPath
        {
            get => _logoPath;
            set
            {
                if (SetProperty(ref _logoPath, value))
                    OnPropertyChanged(nameof(HasLogo));
            }
        }

        public bool HasLogo => !string.IsNullOrWhiteSpace(_logoPath);

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                // Normalize to start of day
                var normalizedValue = value.Date;
                if (SetProperty(ref _startDate, normalizedValue))
                {
                    _ = RefreshMetricsAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                // Normalize to end of day
                var normalizedValue = value.Date.AddDays(1).AddTicks(-1);
                if (SetProperty(ref _endDate, normalizedValue))
                {
                    _ = RefreshMetricsAsync();
                }
            }
        }

        // Child ViewModels
        public InventoryViewModel InventoryViewModel { get; }
        public SalesViewModel SalesViewModel { get; }
        public ExpensesViewModel ExpensesViewModel { get; }
        public ReportsViewModel ReportsViewModel { get; }
        public AdjustmentsViewModel AdjustmentsViewModel { get; }

        // Commands
        public ICommand RefreshMetricsCommand { get; }
        public ICommand SyncShopifyCommand { get; }

        public MainViewModel(
            CalculationService calculationService,
            ShopifyService shopifyService,
            IDialogService dialogService,
            BusinessSettingsService settingsService,
            InventoryViewModel inventoryViewModel,
            SalesViewModel salesViewModel,
            ExpensesViewModel expensesViewModel,
            ReportsViewModel reportsViewModel,
            AdjustmentsViewModel adjustmentsViewModel)
        {
            _calculationService = calculationService;
            _shopifyService = shopifyService;
            _dialogService = dialogService;
            _settingsService = settingsService;

            InventoryViewModel = inventoryViewModel;
            SalesViewModel = salesViewModel;
            ExpensesViewModel = expensesViewModel;
            ReportsViewModel = reportsViewModel;
            AdjustmentsViewModel = adjustmentsViewModel;

            // Initialize date range to current year
            var now = DateTime.Now;
            _startDate = new DateTime(now.Year, 1, 1); // January 1 of current year
            _endDate = new DateTime(now.Year, 12, 31, 23, 59, 59, 999).AddTicks(9999); // December 31 of current year, end of day

            RefreshMetricsCommand = new RelayCommand(async () => await RefreshMetricsAsync());
            SyncShopifyCommand = new RelayCommand(async () => await SyncShopifyAsync());

            // Subscribe to calculation updates
            _calculationService.MetricsUpdated += OnMetricsUpdated;

            // Subscribe to Shopify events
            _shopifyService.SyncStatusChanged += OnShopifySyncStatusChanged;

            // A sync (from the dashboard button or the Shopify Settings dialog) changes
            // inventory and sales underneath the open tabs — reload them so the lists aren't stale.
            _shopifyService.SyncCompleted += OnShopifySyncCompleted;

            // Keep the dashboard logo in sync with Business Settings.
            _settingsService.SettingsChanged += OnBusinessSettingsChanged;

            // Initial load
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading...";

            try
            {
                await _shopifyService.InitializeAsync();
                await RefreshMetricsAsync();
                await LoadBusinessLogoAsync();
                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshMetricsAsync()
        {
            IsLoading = true;
            try
            {
                var metrics = await _calculationService.RecalculateMetricsAsync(StartDate, EndDate);
                UiDispatcher.Run(() => CurrentMetrics = metrics);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to refresh metrics");
                UiDispatcher.Run(() => StatusMessage = "Failed to load metrics");
            }
            finally
            {
                UiDispatcher.Run(() => IsLoading = false);
            }
        }

        private async Task SyncShopifyAsync()
        {
            // Check if Shopify is configured
            if (!_shopifyService.IsConfigured)
            {
                await _dialogService.ShowAlertAsync(
                    "Shopify Not Configured",
                    "Shopify sync isn't set up yet.\n\n" +
                    "Open Shopify Settings (next to this button on desktop, or Settings → " +
                    "Shopify Sync on mobile), turn the integration on, and enter your store " +
                    "name and Admin API access token. There's a Test Connection button to " +
                    "verify before syncing.");
                return;
            }

            IsLoading = true;
            StatusMessage = "Syncing with Shopify...";

            try
            {
                // Test connection first
                var connected = await _shopifyService.TestConnectionAsync();
                if (!connected)
                {
                    StatusMessage = "Failed to connect to Shopify";
                    await _dialogService.ShowAlertAsync(
                        "Connection Failed",
                        "Could not connect to your Shopify store.\n\n" +
                        "Please check:\n" +
                        "• Store name is correct (without .myshopify.com)\n" +
                        "• Access token is valid\n" +
                        "• The app has required permissions");
                    return;
                }

                await _shopifyService.SyncProductsAsync();
                await _shopifyService.SyncOrdersAsync();
                await RefreshMetricsAsync();
                StatusMessage = "Shopify sync completed";

                await _dialogService.ShowAlertAsync(
                    "Sync Complete",
                    "Shopify sync completed successfully!");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
                await _dialogService.ShowAlertAsync(
                    "Sync Error",
                    $"Shopify sync failed:\n\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadBusinessLogoAsync()
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                UiDispatcher.Run(() => LogoPath = settings.LogoPath);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load business logo");
            }
        }

        private void OnBusinessSettingsChanged(object? sender, EventArgs e) => _ = LoadBusinessLogoAsync();

        private void OnMetricsUpdated(object? sender, BusinessMetrics metrics)
        {
            // MetricsUpdated is raised from a background timer; marshal to the UI thread.
            UiDispatcher.Run(() => CurrentMetrics = metrics);
        }

        private void OnShopifySyncStatusChanged(object? sender, string status)
        {
            UiDispatcher.Run(() => StatusMessage = status);
        }

        private void OnShopifySyncCompleted(object? sender, EventArgs e)
        {
            UiDispatcher.Run(() =>
            {
                InventoryViewModel?.RefreshCommand.Execute(null);
                SalesViewModel?.RefreshCommand.Execute(null);
                _ = RefreshMetricsAsync();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events to prevent memory leaks
                _calculationService.MetricsUpdated -= OnMetricsUpdated;
                _shopifyService.SyncStatusChanged -= OnShopifySyncStatusChanged;
                _shopifyService.SyncCompleted -= OnShopifySyncCompleted;
                _settingsService.SettingsChanged -= OnBusinessSettingsChanged;

                // Dispose of services
                _calculationService?.Dispose();

                // Dispose of child ViewModels
                InventoryViewModel?.Dispose();
                SalesViewModel?.Dispose();
                ExpensesViewModel?.Dispose();
                ReportsViewModel?.Dispose();
                AdjustmentsViewModel?.Dispose();

                Log.Information("MainViewModel and all child ViewModels disposed");
            }

            base.Dispose(disposing);
        }
    }
}
