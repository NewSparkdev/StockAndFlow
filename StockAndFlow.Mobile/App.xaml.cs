using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Mobile.Platform;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Marshal ViewModel UI-thread updates onto the MAUI main thread (service events and the
		// metrics timer can fire on background threads).
		UiDispatcher.Post = action => MainThread.BeginInvokeOnMainThread(action);

		// Wire credential encryption to the platform SecureStorage-backed provider.
		// Fire-and-forget: Shopify credentials are only needed after the dashboard has loaded.
		_ = MauiCredentialProtector.InitializeAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Show a lightweight loading page immediately so startup returns fast (no ANR),
		// then initialize the database off the UI thread and swap in the real shell.
		var window = new Window(BuildLoadingPage());
		_ = InitializeAsync(window);
		return window;
	}

	private static ContentPage BuildLoadingPage() => new()
	{
		BackgroundColor = Color.FromArgb("#512BD4"),
		Content = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Spacing = 24,
			Children =
			{
				new Image
				{
					Source = "logo.png",
					WidthRequest = 128,
					HeightRequest = 128,
					HorizontalOptions = LayoutOptions.Center
				},
				new Label
				{
					Text = "Stock & Flow",
					TextColor = Colors.White,
					FontSize = 28,
					FontAttributes = FontAttributes.Bold,
					HorizontalTextAlignment = TextAlignment.Center
				},
				new ActivityIndicator
				{
					IsRunning = true,
					Color = Colors.White,
					Margin = new Thickness(0, 16, 0, 0)
				}
			}
		}
	};

	private async Task InitializeAsync(Window window)
	{
		var services = IPlatformApplication.Current!.Services;
		try
		{
			// InitializeAsync offloads the heavy EnsureDatabaseCreated work to the thread pool,
			// so awaiting it here keeps the UI thread responsive.
			await services.GetRequiredService<IDataService>().InitializeAsync();

			// Alert the user when any sale drops an item below its minimum stock level.
			services.GetRequiredService<InventoryService>().LowStockDetected += async (_, item) =>
			{
				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					if (Current?.Windows is { Count: > 0 } wins && wins[0].Page is Page page)
						await page.DisplayAlert("Low stock",
							$"{item.Name} is running low — {item.QuantityOnHand} remaining (minimum: {item.MinimumStockLevel}).",
							"OK");
				});
			};

			var seenOnboarding = Preferences.Default.Get("onboarding_done", false);
			window.Page = seenOnboarding
				? new AppShell(services)
				: new StockAndFlow.Mobile.Pages.OnboardingPage(services);
		}
		catch (Exception ex)
		{
			window.Page = new ContentPage
			{
				Content = new Label
				{
					Text = $"Startup failed:\n\n{ex.Message}",
					Margin = 24,
					VerticalOptions = LayoutOptions.Center
				}
			};
		}
	}
}
