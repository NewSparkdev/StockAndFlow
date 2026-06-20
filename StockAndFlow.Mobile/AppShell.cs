using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Mobile.Pages;

namespace StockAndFlow.Mobile;

/// <summary>
/// Hybrid navigation: a bottom tab bar gives one-tap access to the five primary sections, while the
/// hamburger flyout drawer lists everything (including the secondary Adjustments section). Pages are
/// DI-resolved because each has a constructor dependency on its ViewModel.
/// </summary>
public class AppShell : Shell
{
	public AppShell(IServiceProvider services)
	{
		FlyoutBehavior = FlyoutBehavior.Flyout;
		FlyoutHeader = BuildHeader();

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

	private static View BuildHeader() => new Grid
	{
		HeightRequest = 140,
		BackgroundColor = Color.FromArgb("#512BD4"),
		Children =
		{
			new VerticalStackLayout
			{
				Padding = new Thickness(20),
				VerticalOptions = LayoutOptions.End,
				Spacing = 2,
				Children =
				{
					new Label { Text = "Stock & Flow", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
					new Label { Text = "Inventory & sales", FontSize = 13, TextColor = Color.FromArgb("#D9D2F5") }
				}
			}
		}
	};
}
