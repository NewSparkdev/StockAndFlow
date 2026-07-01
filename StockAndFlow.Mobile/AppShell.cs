using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using StockAndFlow.Mobile.Pages;

namespace StockAndFlow.Mobile;

/// <summary>
/// Hybrid navigation: a bottom tab bar gives one-tap access to the five primary sections, while a
/// Material-3 styled hamburger flyout drawer lists everything (including the secondary Adjustments
/// section). Pages are DI-resolved because each has a constructor dependency on its ViewModel.
/// </summary>
public class AppShell : Shell
{
	public AppShell(IServiceProvider services)
	{
		FlyoutBehavior = FlyoutBehavior.Flyout;
		FlyoutWidth = 304;
		FlyoutHeader = BuildHeader();

		// Modern pill-style flyout items on a fixed dark "sidebar" that flows from the gradient
		// header and stays consistent (and legible) in both light and dark app themes.
		var itemTemplate = (DataTemplate)Application.Current!.Resources["AppFlyoutItem"];
		ItemTemplate = itemTemplate;
		MenuItemTemplate = itemTemplate;
		FlyoutBackgroundColor = Color.FromArgb("#1A1622");

		// Primary sections -> bottom tab bar (and listed individually in the drawer).
		var primary = new FlyoutItem
		{
			Title = "Stock & Flow",
			FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems
		};
		primary.Items.Add(MakeTab("Dashboard", "tab_dashboard.png", services.GetRequiredService<MainPage>()));
		primary.Items.Add(MakeTab("Inventory", "tab_inventory.png", services.GetRequiredService<InventoryPage>()));
		primary.Items.Add(MakeTab("Sales", "tab_sales.png", services.GetRequiredService<SalesPage>()));
		primary.Items.Add(MakeTab("Expenses", "tab_expenses.png", services.GetRequiredService<ExpensesPage>()));
		primary.Items.Add(MakeTab("Reports", "tab_reports.png", services.GetRequiredService<ReportsPage>()));
		Items.Add(primary);

		// Secondary section -> drawer only (keeps the bottom bar at 5, avoiding the "More" overflow).
		Items.Add(new ShellContent
		{
			Title = "Adjustments",
			Icon = "tab_adjustments.png",
			Content = services.GetRequiredService<AdjustmentsPage>()
		});
	}

	private static Tab MakeTab(string title, string icon, Page page)
	{
		var tab = new Tab { Title = title, Icon = icon };
		tab.Items.Add(new ShellContent { Title = title, Icon = icon, Content = page });
		return tab;
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

		// A rounded app-tile badge with a soft ring, so the logo reads as an icon rather than a
		// floating glyph.
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
