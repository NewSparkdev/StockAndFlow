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

	private static Label? _statusLabel;

	private static ContentPage BuildLoadingPage()
	{
		_statusLabel = new Label
		{
			Text = "Starting...",
			TextColor = Color.FromArgb("#AAFFFFFF"),
			FontSize = 13,
			HorizontalTextAlignment = TextAlignment.Center,
		};
		return new ContentPage
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
					},
					_statusLabel
				}
			}
		};
	}

	private static void SetStatus(string message) =>
		MainThread.BeginInvokeOnMainThread(() => { if (_statusLabel != null) _statusLabel.Text = message; });

	private async Task InitializeAsync(Window window)
	{
		var services = IPlatformApplication.Current!.Services;
		try
		{
			SetStatus("Initializing database...");
			var initTask = services.GetRequiredService<IDataService>().InitializeAsync();
			if (await Task.WhenAny(initTask, Task.Delay(45_000)) != initTask)
			{
				window.Page = new ContentPage
				{
					BackgroundColor = Color.FromArgb("#512BD4"),
					Content = new Label
					{
						Text = "Startup timed out — please force-quit and reopen the app.",
						TextColor = Colors.White,
						Margin = 32,
						HorizontalTextAlignment = TextAlignment.Center,
						VerticalOptions = LayoutOptions.Center
					}
				};
				return;
			}
			await initTask;
			SetStatus("Loading...");

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
