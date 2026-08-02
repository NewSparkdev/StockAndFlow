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
        private bool _pushStockLevels;

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

        /// <summary>Send local counts up to Shopify after each sync.</summary>
        public bool PushStockLevels
        {
            get => _pushStockLevels;
            set => SetProperty(ref _pushStockLevels, value);
        }

        public string StockOwnershipHelpText =>
            "Stock & Flow keeps the real stock count.\n\n" +
            "It's the only place that sees everything you sell — market stalls, craft fairs and " +
            "your own shop as well as online orders. Shopify only ever knows about its own " +
            "orders, so its number goes stale the moment you sell one in person.\n\n" +
            "So a sync brings your online orders IN (they're recorded as sales and come off your " +
            "stock), but it never overwrites your counts with Shopify's.\n\n" +
            "Turn on \"Send my stock counts to Shopify\" and your storefront gets updated after " +
            "each sync, so it won't sell something you already sold at a market.\n\n" +
            "If you'd rather start over from Shopify's numbers — say you did a stock-take in " +
            "Shopify's admin — use \"Use Shopify's stock counts\" below. That's a one-off; " +
            "it overwrites your counts, so it asks first.";

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
        public ICommand AdoptShopifyStockCommand { get; }
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
            AdoptShopifyStockCommand = new RelayCommand(async () => await AdoptShopifyStockAsync(), () => !IsBusy);
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
                    PushStockLevels = settings.PushStockLevelsToShopify;
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
            settings.PushStockLevelsToShopify = PushStockLevels;
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

                // Products first (new items appear), then orders (they reference those items).
                // Neither touches local stock counts except by recording the sales themselves.
                await _shopifyService.SyncProductsAsync();
                await _shopifyService.SyncOrdersAsync();

                if (PushStockLevels)
                    await _shopifyService.PushStockLevelsToShopifyAsync();
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

        /// <summary>
        /// One-off: replace local stock counts with Shopify's. Destructive, so it confirms first —
        /// anything sold in person but not yet reflected in Shopify will be lost.
        /// </summary>
        private async Task AdoptShopifyStockAsync()
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

                var confirmed = await _dialogService.ShowConfirmAsync(
                    "Use Shopify's stock counts?",
                    "This replaces your stock counts with the numbers currently in Shopify.\n\n" +
                    "Anything you've sold in person that Shopify doesn't know about will be added " +
                    "back on. Only do this after a stock-take in Shopify.",
                    "Use Shopify's counts",
                    "Cancel");

                if (!confirmed)
                {
                    StatusMessage = "Left your stock counts alone.";
                    return;
                }

                await _shopifyService.SyncProductsAsync(adoptShopifyStockLevels: true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't take Shopify's counts: {ex.Message}";
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
