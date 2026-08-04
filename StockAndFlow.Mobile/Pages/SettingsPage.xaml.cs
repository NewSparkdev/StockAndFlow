using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Mobile.Services;
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

		// Desktop edition has no store billing; Pro users see their status instead of an upsell.
		if (DeviceInfo.Platform == DevicePlatform.WinUI)
		{
			ProHeader.IsVisible = false;
			ProRow.IsVisible = false;
			return;
		}

		var entitlements = IPlatformApplication.Current!.Services.GetRequiredService<EntitlementService>();
		if (await entitlements.IsProAsync())
		{
			ProRowTitle.Text = "Stock & Flow Pro is active";
			ProRowSubtitle.Text = "Thanks for supporting the app! Manage or restore purchases";
		}
	}

	private async void OnUpgradeTapped(object? sender, TappedEventArgs e)
	{
		// The paywall doubles as the plan-status / restore-purchases screen for Pro users.
		await Navigation.PushModalAsync(new NavigationPage(new PaywallPage()));
	}

	private async void OnBusinessSettingsTapped(object? sender, TappedEventArgs e)
	{
		var vm = IPlatformApplication.Current!.Services.GetRequiredService<BusinessSettingsViewModel>();
		// Pushed (not modal) so it gets a top back arrow returning to this Settings menu.
		await Navigation.PushAsync(new BusinessSettingsPage(vm));
	}

	private async void OnShopifySettingsTapped(object? sender, TappedEventArgs e)
	{
		var entitlements = IPlatformApplication.Current!.Services.GetRequiredService<EntitlementService>();
		if (!await entitlements.EnsureProAsync("Shopify sync is a Pro feature."))
			return;

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
		// Export is always free; the page itself meters imports monthly.
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
