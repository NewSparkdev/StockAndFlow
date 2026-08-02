using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    /// <summary>
    /// Configure + operate the Shopify integration: store handle, Admin API access token,
    /// connection test, and manual sync. Credentials are persisted through AppSettings, whose
    /// accessors encrypt via SecureCredentialService.
    /// </summary>
    public class ShopifySettingsViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly ShopifyService _shopifyService;
        private readonly IDialogService _dialogService;

        private bool _shopifyEnabled;
        private string? _storeName;
        private string? _accessToken;
        private string? _statusMessage;
        private bool _isBusy;

        public event EventHandler<bool>? CloseRequested;

        public bool ShopifyEnabled
        {
            get => _shopifyEnabled;
            set => SetProperty(ref _shopifyEnabled, value);
        }

        public string? StoreName
        {
            get => _storeName;
            set => SetProperty(ref _storeName, value);
        }

        public string? AccessToken
        {
            get => _accessToken;
            set => SetProperty(ref _accessToken, value);
        }

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

        public string StoreNameHelpText =>
            "Your store's handle — the part before .myshopify.com. " +
            "Example: if your admin URL is candleco.myshopify.com, enter candleco. " +
            "Pasting the full address works too.";

        public string AccessTokenHelpText =>
            "In your Shopify admin: Settings → Apps and sales channels → Develop apps → " +
            "Create an app → give it read_products, read_orders, and write_inventory scopes → " +
            "Install → copy the Admin API access token (starts with shpat_). " +
            "The token is stored encrypted and never leaves this device except to talk to Shopify.";

        public ICommand TestConnectionCommand { get; }
        public ICommand SyncNowCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ShopifySettingsViewModel(
            IDataService dataService,
            ShopifyService shopifyService,
            IDialogService dialogService)
        {
            _dataService = dataService;
            _shopifyService = shopifyService;
            _dialogService = dialogService;

            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => !IsBusy);
            SyncNowCommand = new RelayCommand(async () => await SyncNowAsync(), () => !IsBusy);
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(Cancel);

            _shopifyService.SyncStatusChanged += (_, message) =>
                UiDispatcher.Run(() => StatusMessage = message);

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var settings = await _dataService.GetSettingsAsync();
                UiDispatcher.Run(() =>
                {
                    ShopifyEnabled = settings.ShopifyEnabled;
                    StoreName = settings.ShopifyStoreName;
                    AccessToken = settings.ShopifyAccessToken;
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load Shopify settings");
            }
        }

        /// <summary>Persists the credentials and re-points the service at them.</summary>
        private async Task PersistAsync()
        {
            var settings = await _dataService.GetSettingsAsync();
            settings.ShopifyEnabled = ShopifyEnabled;
            settings.ShopifyStoreName = StoreName?.Trim();
            settings.ShopifyAccessToken = AccessToken?.Trim();
            await _dataService.SaveSettingsAsync(settings);
            await _shopifyService.InitializeAsync();
        }

        private async Task SaveAsync()
        {
            try
            {
                IsBusy = true;
                await PersistAsync();
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to save Shopify settings");
                await _dialogService.ShowAlertAsync("Save failed", $"Could not save Shopify settings:\n\n{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Testing connection...";
                await PersistAsync();

                if (!_shopifyService.IsConfigured)
                {
                    StatusMessage = "Enter a store name and access token, and turn the integration on.";
                    return;
                }

                var ok = await _shopifyService.TestConnectionAsync();
                StatusMessage = ok
                    ? "✓ Connected — your store answered."
                    : "✗ Could not reach the store. Check the store name and access token.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Connection test failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SyncNowAsync()
        {
            try
            {
                IsBusy = true;
                await PersistAsync();

                if (!_shopifyService.IsConfigured)
                {
                    StatusMessage = "Enter a store name and access token, and turn the integration on.";
                    return;
                }

                await _shopifyService.SyncProductsAsync();
                await _shopifyService.SyncOrdersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
