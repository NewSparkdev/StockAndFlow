using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using StockAndFlow.Mobile.Pages;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile;

/// <summary>
/// The Shell hosts a single <see cref="HostPage"/> (persistent bottom bar + swappable sections).
/// The hamburger flyout drawer lists everything and drives the same host, so navigation never
/// re-creates the bar. Adjustments is a secondary section reached only from the drawer (pushed).
/// </summary>
public class AppShell : Shell
{
	private readonly IServiceProvider _services;
	private readonly HostPage _host;
	private readonly BusinessSettingsService _settings;
	private readonly Dictionary<string, Border> _drawerRows = new();
	private Image? _drawerLogo;

	public AppShell(IServiceProvider services)
	{
		_services = services;
		FlyoutBehavior = FlyoutBehavior.Flyout;
		FlyoutWidth = 304;
		FlyoutBackgroundColor = Color.FromArgb("#1A1622");

		_host = services.GetRequiredService<HostPage>();
		_host.SectionChanged += HighlightDrawer;

		_settings = services.GetRequiredService<BusinessSettingsService>();
		_settings.SettingsChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshDrawerLogoAsync());

		FlyoutHeader = BuildHeader();
		FlyoutContent = BuildFlyout();

		Items.Add(new ShellContent { Title = "Stock & Flow", Content = _host });

		HighlightDrawer(_host.CurrentSection);
		_ = RefreshDrawerLogoAsync();
	}

	private View BuildFlyout()
	{
		var list = new VerticalStackLayout { Padding = new Thickness(10, 12), Spacing = 4 };

		list.Add(MakeRow("dashboard", "Dashboard", "tab_dashboard.png", () => _host.SelectSection("dashboard")));
		list.Add(MakeRow("inventory", "Inventory", "tab_inventory.png", () => _host.SelectSection("inventory")));
		list.Add(MakeRow("sales", "Sales", "tab_sales.png", () => _host.SelectSection("sales")));
		list.Add(MakeRow("expenses", "Expenses", "tab_expenses.png", () => _host.SelectSection("expenses")));
		list.Add(MakeRow("reports", "Reports", "tab_reports.png", () => _host.SelectSection("reports")));

		list.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#33FFFFFF"), Margin = new Thickness(8, 8) });

		list.Add(MakeRow("customers", "Customers", "tab_sales.png", PushCustomers));
		list.Add(MakeRow("adjustments", "Adjustments", "tab_adjustments.png", PushAdjustments));

		// Business logo (set in Settings > Business Settings), shown under the menu items.
		_drawerLogo = new Image
		{
			Aspect = Aspect.AspectFit,
			HeightRequest = 90,
			Margin = new Thickness(14, 24, 14, 0),
			HorizontalOptions = LayoutOptions.Center,
			IsVisible = false
		};
		list.Add(_drawerLogo);

		return new ScrollView { Content = list };
	}

	private async Task RefreshDrawerLogoAsync()
	{
		if (_drawerLogo == null)
			return;
		try
		{
			var settings = await _settings.GetSettingsAsync();
			var path = ResolveImagePath(settings.LogoPath);
			_drawerLogo.Source = path;
			_drawerLogo.IsVisible = path != null;
		}
		catch
		{
			_drawerLogo.IsVisible = false;
		}
	}

	// Mirrors ImagePathConverter: re-base a stored logo path to the current app Images directory.
	private static string? ResolveImagePath(string? stored)
	{
		if (string.IsNullOrWhiteSpace(stored))
			return null;
		try
		{
			var fileName = System.IO.Path.GetFileName(stored);
			if (!string.IsNullOrEmpty(fileName))
			{
				var rebased = System.IO.Path.Combine(FileSystem.AppDataDirectory, "Images", fileName);
				if (File.Exists(rebased))
					return rebased;
			}
		}
		catch
		{
			// fall through
		}
		return File.Exists(stored) ? stored : null;
	}

	private Border MakeRow(string id, string title, string icon, Action onTap)
	{
		var row = new Border
		{
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Padding = new Thickness(14, 12),
			Content = new HorizontalStackLayout
			{
				Spacing = 16,
				Children =
				{
					new Image { Source = icon, WidthRequest = 22, HeightRequest = 22, VerticalOptions = LayoutOptions.Center },
					new Label { Text = title, TextColor = Colors.White, FontSize = 15, VerticalOptions = LayoutOptions.Center }
				}
			}
		};
		row.GestureRecognizers.Add(new TapGestureRecognizer
		{
			Command = new Command(() =>
			{
				onTap();
				FlyoutIsPresented = false;
			})
		});
		_drawerRows[id] = row;
		return row;
	}

	private async void PushCustomers() =>
		await Navigation.PushAsync(_services.GetRequiredService<Pages.CustomersPage>());

	private async void PushAdjustments() =>
		await Navigation.PushAsync(_services.GetRequiredService<AdjustmentsPage>());

	private void HighlightDrawer(string activeId)
	{
		foreach (var (id, row) in _drawerRows)
			row.BackgroundColor = id == activeId ? Color.FromArgb("#3B2E6B") : Colors.Transparent;
	}

	private static View BuildHeader()
	{
		var header = new Grid
		{
			HeightRequest = 150,
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0),
				EndPoint = new Point(1, 1),
				GradientStops =
				{
					new GradientStop(Color.FromArgb("#6242DD"), 0f),
					new GradientStop(Color.FromArgb("#2B0B98"), 1f)
				}
			}
		};

		var badge = new Border
		{
			WidthRequest = 60,
			HeightRequest = 60,
			Stroke = Color.FromArgb("#40FFFFFF"),
			StrokeThickness = 1,
			BackgroundColor = Color.FromArgb("#512BD4"),
			StrokeShape = new RoundRectangle { CornerRadius = 16 },
			VerticalOptions = LayoutOptions.Center,
			Content = new Image
			{
				Source = "logo.png",
				WidthRequest = 38,
				HeightRequest = 38,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center
			}
		};

		var text = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Spacing = 3,
			Children =
			{
				new Label { Text = "Stock & Flow", FontFamily = "OpenSansSemibold", FontSize = 21, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
				new Label { Text = "Inventory & sales", FontSize = 13, TextColor = Color.FromArgb("#DCD4F7") }
			}
		};

		header.Add(new HorizontalStackLayout
		{
			Spacing = 15,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Children = { badge, text }
		});

		return header;
	}
}
