using System.IO;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using StockAndFlow.Data;
using StockAndFlow.Mobile.Platform;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;
namespace StockAndFlow.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});


		var services = builder.Services;

		// Platform abstractions (MAUI implementations of the Core interfaces)
		var pathProvider = new MauiPathProvider();
		services.AddSingleton<IPathProvider>(pathProvider);
		services.AddSingleton<IDialogService, MauiDialogService>();
		services.AddSingleton<IFilePickerService, MauiFilePickerService>();
		services.AddSingleton<IEditorPresenter, MauiEditorPresenter>();

		// Data layer - SQLite under the app sandbox
		var databasePath = Path.Combine(pathProvider.DataDirectory, "stockandflow.db");
		var settingsPath = Path.Combine(pathProvider.DataDirectory, "settings.json");
		services.AddSingleton<IDataService>(_ => new SQLiteDataService(databasePath, settingsPath));

		// Business services
		services.AddSingleton<InventoryService>();
		services.AddSingleton<BomService>();
		services.AddSingleton<SalesService>();
		services.AddSingleton<ExpenseService>();
		services.AddSingleton<CalculationService>();
		services.AddSingleton<ShopifyService>();
		// Free/Pro gating. FreeEntitlementProvider until the store billing layer lands, so
		// nothing is silently unlocked in the meantime.
		services.AddSingleton<IEntitlementProvider, FreeEntitlementProvider>();
		services.AddSingleton<IPurchaseService, UnavailablePurchaseService>();
		services.AddSingleton<IPaywallPresenter>(sp => new MauiPaywallPresenter(sp));
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
		services.AddSingleton<CustomerService>();

		// ViewModels
		services.AddSingleton<MainViewModel>();
		services.AddSingleton<InventoryViewModel>();
		services.AddSingleton<SalesViewModel>();
		services.AddSingleton<ExpensesViewModel>();
		services.AddSingleton<ReportsViewModel>();
		services.AddSingleton<AdjustmentsViewModel>();
		services.AddSingleton<CustomersViewModel>();
		services.AddTransient<BusinessSettingsViewModel>();
		services.AddTransient<ShopifySettingsViewModel>();
		services.AddTransient<AddEditCustomerViewModel>();

		// Pages
		services.AddSingleton<MainPage>();
		services.AddSingleton<Pages.InventoryPage>();
		services.AddSingleton<Pages.SalesPage>();
		services.AddSingleton<Pages.ExpensesPage>();
		services.AddSingleton<Pages.AdjustmentsPage>();
		services.AddSingleton<Pages.ReportsPage>();
		services.AddSingleton<Pages.CustomersPage>();

		// Single host page: persistent bottom bar + swappable section views.
		services.AddSingleton<HostPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Note: database initialization happens asynchronously in App.CreateWindow (behind a loading
		// screen) so the UI thread is never blocked during startup (which would trigger an ANR).
		return builder.Build();
	}
}
