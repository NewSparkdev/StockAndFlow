using Microsoft.Extensions.DependencyInjection;
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
