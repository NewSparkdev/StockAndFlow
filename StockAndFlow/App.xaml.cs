using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // Initialize data service asynchronously (don't block UI thread)
                var dataService = _serviceProvider.GetRequiredService<IDataService>();
                _ = dataService.InitializeAsync();

                var mainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
                };
                mainWindow.Show();
            }
            catch (System.Exception ex)
            {
                var errorMessage = $"Startup Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nInner Exception: {ex.InnerException?.Message}";
                System.IO.File.WriteAllText(@"C:\Dev\StockAndFlow\error.log", errorMessage);
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Data Service
            services.AddSingleton<IDataService, DataService>();

            // Business Services
            services.AddSingleton<InventoryService>();
            services.AddSingleton<SalesService>();
            services.AddSingleton<ExpenseService>();
            services.AddSingleton<CalculationService>();
            services.AddSingleton<ShopifyService>();
            services.AddSingleton<ExportImportService>();
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
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

