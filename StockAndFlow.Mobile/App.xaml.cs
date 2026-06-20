using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Mobile.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

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
			Spacing = 16,
			Children =
			{
				new ActivityIndicator { IsRunning = true, Color = Colors.White },
				new Label { Text = "Loading Stock & Flow…", TextColor = Colors.White }
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
			window.Page = new AppShell(services);
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
