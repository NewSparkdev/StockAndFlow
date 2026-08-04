using System.Windows;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StockAndFlow.Data;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;
using StockAndFlow.Wpf.Platform;

namespace StockAndFlow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        /// <summary>
        /// Application service provider, exposed for the rare WPF code-behind that must resolve
        /// a ViewModel directly (e.g. RecordSaleDialog opening Business Settings).
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize logging first
            LoggingService.Initialize();

            // Wire credential encryption to the Windows DPAPI provider before any settings are loaded
            // (AppSettings.MigrateToEncrypted relies on it during LoadSettingsAsync below).
            SecureCredentialService.Provider = new WpfCredentialProtector();

            // Marshal ViewModel UI-thread updates through the WPF dispatcher.
            UiDispatcher.Post = action => Current.Dispatcher.Invoke(action);

            try
            {
                Log.Information("Application startup initiated");

                // Load settings to determine storage mode
                var settings = await LoadSettingsAsync();

                var services = new ServiceCollection();
                ConfigureServices(services, settings);
                _serviceProvider = services.BuildServiceProvider();
                Services = _serviceProvider;

                // Initialize data service
                var dataService = _serviceProvider.GetRequiredService<IDataService>();
                await dataService.InitializeAsync();

                // Handle migration from JSON to SQLite if needed
                if (settings.StorageMode == StorageMode.SQLite)
                {
                    await HandleMigrationAsync();
                }

                var mainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
                };
                mainWindow.Show();
            }
            catch (System.Exception ex)
            {
                Log.Fatal(ex, "Fatal error during application startup");

                var errorMessage = $"A critical error occurred during startup.\n\n{ex.Message}\n\nPlease check the log files in the Logs folder for details.";
                MessageBox.Show(errorMessage, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

                LoggingService.Shutdown();
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services, AppSettings settings)
        {
            // Platform abstractions (WPF implementations of the Core interfaces)
            services.AddSingleton<IPathProvider, WpfPathProvider>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<IFilePickerService, WpfFilePickerService>();
            services.AddSingleton<IEditorPresenter, WpfEditorPresenter>();

            // Data Service - register based on storage mode
            if (settings.StorageMode == StorageMode.SQLite)
            {
                var dataDir = settings.DataStoragePath;
                if (!Path.IsPathRooted(dataDir))
                {
                    dataDir = Path.Combine(Directory.GetCurrentDirectory(), dataDir);
                }

                var databasePath = Path.Combine(dataDir, "stockandflow.db");
                var settingsPath = Path.Combine(dataDir, "settings.json");

                services.AddSingleton<IDataService>(sp => new SQLiteDataService(databasePath, settingsPath));
            }
            else
            {
                services.AddSingleton<IDataService, DataService>();
            }

            // Business Services
            services.AddSingleton<InventoryService>();
            services.AddSingleton<BomService>();
            services.AddSingleton<SalesService>();
            services.AddSingleton<ExpenseService>();
            services.AddSingleton<CalculationService>();
            services.AddSingleton<ShopifyService>();
            // Free/Pro gating. FreeEntitlementProvider until desktop licence keys land.
            services.AddSingleton<IEntitlementProvider, FreeEntitlementProvider>();
            services.AddSingleton<IPurchaseService, UnavailablePurchaseService>();
            services.AddSingleton<IPaywallPresenter>(sp => new WpfPaywallPresenter(sp));
            services.AddSingleton<EntitlementService>();
            services.AddSingleton<ExportImportService>(sp =>
            {
                var svc = new ExportImportService(
                    sp.GetRequiredService<IDataService>(),
                    sp.GetRequiredService<InventoryService>(),
                    sp.GetRequiredService<SalesService>(),
                    sp.GetRequiredService<ExpenseService>(),
                    sp.GetRequiredService<IPathProvider>());
                // Assigned rather than injected: EntitlementService also needs InventoryService,
                // and constructor injection here would form a cycle.
                svc.Entitlements = sp.GetRequiredService<EntitlementService>();
                return svc;
            });
            services.AddSingleton<BusinessSettingsService>();
            services.AddSingleton<InvoiceService>();
            services.AddSingleton<InventoryAdjustmentService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<InventoryViewModel>();
            services.AddSingleton<SalesViewModel>();
            services.AddSingleton<ExpensesViewModel>();
            services.AddSingleton<ReportsViewModel>();
            services.AddSingleton<AdjustmentsViewModel>();

            // Transient editor ViewModel resolved directly by RecordSaleDialog's "edit settings" path.
            services.AddTransient<BusinessSettingsViewModel>();
            services.AddTransient<ShopifySettingsViewModel>();
        }

        private async Task<AppSettings> LoadSettingsAsync()
        {
            return await Task.Run(() =>
            {
                var dataDir = "Data";
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }

                var settingsPath = Path.Combine(dataDir, "settings.json");

                if (!File.Exists(settingsPath))
                {
                    // Create default settings with SQLite as default
                    var defaultSettings = new AppSettings
                    {
                        StorageMode = StorageMode.SQLite
                    };

                    var json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true
                    });

                    File.WriteAllText(settingsPath, json);
                    return defaultSettings;
                }

                var settingsJson = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AppSettings { StorageMode = StorageMode.SQLite };

                // Migrate any plain-text credentials to encrypted format
                var needsSave = false;
                var beforeMigration = new
                {
                    StoreName = settings.ShopifyStoreNameEncrypted,
                    ApiKey = settings.ShopifyApiKeyEncrypted,
                    Token = settings.ShopifyAccessTokenEncrypted
                };

                settings.MigrateToEncrypted();

                // Check if migration actually changed anything
                if (beforeMigration.StoreName != settings.ShopifyStoreNameEncrypted ||
                    beforeMigration.ApiKey != settings.ShopifyApiKeyEncrypted ||
                    beforeMigration.Token != settings.ShopifyAccessTokenEncrypted)
                {
                    needsSave = true;
                }

                // Save if credentials were migrated
                if (needsSave)
                {
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true
                    });
                    File.WriteAllText(settingsPath, json);
                }

                return settings;
            });
        }

        private async Task HandleMigrationAsync()
        {
            var dataDir = "Data";
            var databasePath = Path.Combine(dataDir, "stockandflow.db");
            var context = new StockAndFlowDbContext(databasePath);
            var migrationService = new DataMigrationService(dataDir, context);

            // Check if we have JSON data and database is empty
            var hasJsonData = await migrationService.HasJsonDataAsync();
            var hasDatabaseData = await migrationService.HasDatabaseDataAsync();

            if (hasJsonData && !hasDatabaseData)
            {
                var result = MessageBox.Show(
                    "JSON data files detected. Would you like to migrate your existing data to SQLite database?\n\n" +
                    "This will:\n" +
                    "• Import all your existing data into the SQLite database\n" +
                    "• Create a backup of your JSON files\n" +
                    "• Keep your original JSON files intact\n\n" +
                    "Click Yes to migrate now, or No to start with an empty database.",
                    "Data Migration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var migrationResult = await migrationService.MigrateFromJsonAsync();

                    if (migrationResult.Success)
                    {
                        MessageBox.Show(
                            $"Migration completed successfully!\n\n" +
                            $"Migrated Records:\n" +
                            $"• Inventory Items: {migrationResult.InventoryItemsMigrated}\n" +
                            $"• Sales: {migrationResult.SalesMigrated}\n" +
                            $"• Expenses: {migrationResult.ExpensesMigrated}\n" +
                            $"• Adjustments: {migrationResult.AdjustmentsMigrated}\n" +
                            $"• Business Settings: {migrationResult.BusinessSettingsMigrated}\n\n" +
                            $"Total: {migrationResult.TotalRecordsMigrated} records\n\n" +
                            $"Your JSON files have been backed up in the Data folder.",
                            "Migration Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Migration failed: {migrationResult.Message}",
                            "Migration Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }

            context.Dispose();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application exit requested");
            _serviceProvider?.Dispose();
            LoggingService.Shutdown();
            base.OnExit(e);
        }

        public IDataService GetDataService()
        {
            return _serviceProvider?.GetRequiredService<IDataService>()
                ?? throw new InvalidOperationException("Service provider not initialized");
        }

        public BusinessSettingsService GetBusinessSettingsService()
        {
            return _serviceProvider?.GetRequiredService<BusinessSettingsService>()
                ?? throw new InvalidOperationException("Service provider not initialized");
        }
    }
}

