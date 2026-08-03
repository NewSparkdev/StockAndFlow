using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();
		VersionLabel.Text = $"Version {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshPlanRowAsync();
	}

	/// <summary>
	/// Shows what's left of the free allowances, so the limits are discoverable before someone
	/// runs into one mid-sale.
	/// </summary>
	private async Task RefreshPlanRowAsync()
	{
		try
		{
			var entitlements = IPlatformApplication.Current!.Services.GetRequiredService<EntitlementService>();
			if (entitlements.IsPro)
			{
				PlanTitleLabel.Text = "Stock & Flow Pro";
				PlanSubtitleLabel.Text = "Active — thank you!";
				return;
			}

			var slots = await entitlements.InventorySlotsRemainingAsync();
			var invoices = await entitlements.InvoicesRemainingThisMonthAsync();

			PlanTitleLabel.Text = "Free plan — see Pro";
			PlanSubtitleLabel.Text =
				$"{slots} of {EntitlementService.FreeInventoryItemLimit} item slots left · {invoices} invoices left this month";
		}
		catch
		{
			// Cosmetic only — never let the settings list fail to render over this.
			PlanTitleLabel.Text = "Stock & Flow Pro";
			PlanSubtitleLabel.Text = "Unlimited everything, plus Shopify sync";
		}
	}

	private async void OnPlanTapped(object? sender, TappedEventArgs e)
	{
		var services = IPlatformApplication.Current!.Services;
		var vm = new PaywallViewModel(
			services.GetRequiredService<EntitlementService>(),
			services.GetRequiredService<IDialogService>(),
			services.GetRequiredService<IPurchaseService>());
		await Navigation.PushAsync(new PaywallPage(vm));
	}

	private async void OnBusinessSettingsTapped(object? sender, TappedEventArgs e)
	{
		var vm = IPlatformApplication.Current!.Services.GetRequiredService<BusinessSettingsViewModel>();
		// Pushed (not modal) so it gets a top back arrow returning to this Settings menu.
		await Navigation.PushAsync(new BusinessSettingsPage(vm));
	}

	private async void OnShopifySettingsTapped(object? sender, TappedEventArgs e)
	{
		var vm = IPlatformApplication.Current!.Services.GetRequiredService<ShopifySettingsViewModel>();
		await Navigation.PushAsync(new ShopifySettingsPage(vm));
	}

	private async void OnBarcodeLabelsTapped(object? sender, TappedEventArgs e)
	{
		var inventory = IPlatformApplication.Current!.Services.GetRequiredService<InventoryService>();
		await Navigation.PushAsync(new BarcodeLabelsPage(inventory));
	}

	private async void OnExportImportTapped(object? sender, TappedEventArgs e)
	{
		var service = IPlatformApplication.Current!.Services.GetRequiredService<ExportImportService>();
		await Navigation.PushModalAsync(new NavigationPage(new ExportImportPage(service)));
	}

	private async void OnAboutTapped(object? sender, TappedEventArgs e)
	{
		await DisplayAlert(
			"Stock & Flow",
			$"Inventory & sales management.\n\nVersion {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})",
			"OK");
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}
}
