using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CalculationService _calculationService;
        private readonly ShopifyService _shopifyService;

        private BusinessMetrics? _currentMetrics;
        private string _statusMessage = "Ready";
        private bool _isLoading;
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
            InventoryViewModel inventoryViewModel,
            SalesViewModel salesViewModel,
            ExpensesViewModel expensesViewModel,
            ReportsViewModel reportsViewModel,
            AdjustmentsViewModel adjustmentsViewModel)
        {
            _calculationService = calculationService;
            _shopifyService = shopifyService;

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
            SyncShopifyCommand = new RelayCommand(SyncShopify);

            // Subscribe to calculation updates
            _calculationService.MetricsUpdated += OnMetricsUpdated;

            // Subscribe to Shopify events
            _shopifyService.SyncStatusChanged += OnShopifySyncStatusChanged;

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
            CurrentMetrics = await _calculationService.RecalculateMetricsAsync(StartDate, EndDate);
            IsLoading = false;
        }

        private void SyncShopify()
        {
            System.Windows.MessageBox.Show(
                "Shopify integration is coming soon!\n\nThis feature will allow you to:\n• Sync products with your Shopify store\n• Import orders automatically\n• Keep inventory levels synchronized",
                "Feature Coming Soon",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private async Task SyncShopifyAsync()
        {
            IsLoading = true;
            StatusMessage = "Syncing with Shopify...";

            try
            {
                await _shopifyService.SyncProductsAsync();
                await _shopifyService.SyncOrdersAsync();
                await RefreshMetricsAsync();
                StatusMessage = "Shopify sync completed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnMetricsUpdated(object? sender, BusinessMetrics metrics)
        {
            CurrentMetrics = metrics;
        }

        private void OnShopifySyncStatusChanged(object? sender, string status)
        {
            StatusMessage = status;
        }
    }
}
