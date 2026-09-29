using StockAndFlow.Mobile.Services;

namespace StockAndFlow.Mobile.Pages;

public partial class PaywallPage : ContentPage
{
	private const string PrivacyPolicyUrl = "https://www.newspark.dev/privacy";
	// No custom EULA: Apple's standard one on iOS, Google Play's terms on Android.
	private const string AppleStandardEulaUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
	private const string GooglePlayTermsUrl = "https://play.google.com/about/play-terms/";

	private readonly EntitlementService _entitlements;
	private bool _busy;

	public PaywallPage(string? reason = null)
	{
		InitializeComponent();
		_entitlements = IPlatformApplication.Current!.Services.GetRequiredService<EntitlementService>();

		if (!string.IsNullOrWhiteSpace(reason))
		{
			ReasonLabel.Text = reason;
			ReasonLabel.IsVisible = true;
		}

		var account = DeviceInfo.Platform == DevicePlatform.iOS ? "Apple Account" : "Google Play account";
		RenewalLabel.Text =
			$"Subscriptions renew automatically at the price shown unless cancelled at least 24 hours before " +
			$"the end of the current period. Payment is charged to your {account}. Manage or cancel anytime " +
			$"in your {account} settings.";
	}

	private async void OnPrivacyTapped(object? sender, TappedEventArgs e) =>
		await OpenLinkAsync(PrivacyPolicyUrl);

	private async void OnTermsTapped(object? sender, TappedEventArgs e) =>
		await OpenLinkAsync(DeviceInfo.Platform == DevicePlatform.iOS ? AppleStandardEulaUrl : GooglePlayTermsUrl);

	private async Task OpenLinkAsync(string url)
	{
		try
		{
			await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
		}
		catch
		{
			await DisplayAlert("Couldn't open link", url, "OK");
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		// Best effort: replace the static price labels with store-localized prices.
		var prices = await _entitlements.GetDisplayPricesAsync();
		if (prices.TryGetValue(EntitlementService.YearlyProductId, out var yearly))
			YearlyPriceLabel.Text = $"{yearly} / year";
		if (prices.TryGetValue(EntitlementService.MonthlyProductId, out var monthly))
			MonthlyPriceLabel.Text = $"{monthly} / month";
		if (prices.TryGetValue(EntitlementService.LifetimeProductId, out var lifetime))
			LifetimePriceLabel.Text = $"{lifetime} one time";
	}

	private async void OnYearlyClicked(object? sender, EventArgs e) =>
		await PurchaseAsync(EntitlementService.YearlyProductId);

	private async void OnMonthlyClicked(object? sender, EventArgs e) =>
		await PurchaseAsync(EntitlementService.MonthlyProductId);

	private async void OnLifetimeClicked(object? sender, EventArgs e) =>
		await PurchaseAsync(EntitlementService.LifetimeProductId);

	private async Task PurchaseAsync(string productId)
	{
		if (_busy) return;
		SetBusy(true);
		try
		{
			var (success, error) = await _entitlements.PurchaseAsync(productId);
			if (success)
			{
				await DisplayAlert("Welcome to Pro!", "All Pro features are now unlocked.", "OK");
				await Navigation.PopModalAsync();
			}
			else if (error != null)
			{
				await DisplayAlert("Purchase Failed", error, "OK");
			}
		}
		finally
		{
			SetBusy(false);
		}
	}

	private async void OnRestoreClicked(object? sender, EventArgs e)
	{
		if (_busy) return;
		SetBusy(true);
		try
		{
			var (success, error) = await _entitlements.RestoreAsync();
			if (success)
			{
				await DisplayAlert("Purchases Restored", "Welcome back — Pro is active again.", "OK");
				await Navigation.PopModalAsync();
			}
			else if (error != null)
			{
				await DisplayAlert("Restore", error, "OK");
			}
		}
		finally
		{
			SetBusy(false);
		}
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		if (!_busy)
			await Navigation.PopModalAsync();
	}

	private void SetBusy(bool busy)
	{
		_busy = busy;
		BusySpinner.IsRunning = busy;
		BusySpinner.IsVisible = busy;
	}
}
