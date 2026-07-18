using Microsoft.Maui.Controls.Shapes;
using StockAndFlow.Mobile.Controls;
using StockAndFlow.Mobile.Pages;

namespace StockAndFlow.Mobile;

/// <summary>
/// The single page shown by the Shell. It holds ONE persistent bottom bar plus a content area that
/// swaps between the five section views. Because nothing is re-created when you switch tabs, the bar
/// never moves — fixing the jump that came from each page owning its own bar copy.
/// </summary>
public class HostPage : ContentPage
{
	private readonly Dictionary<string, View> _sections = new();
	private readonly ContentView _sectionHost = new();
	private readonly BottomNavBar _bar = new();

	/// <summary>Current section id changed — used to keep the flyout drawer's highlight in sync.</summary>
	public event Action<string>? SectionChanged;

	public string CurrentSection { get; private set; } = "dashboard";

	public HostPage(
		MainPage dashboard,
		InventoryPage inventory,
		SalesPage sales,
		ExpensesPage expenses,
		ReportsPage reports)
	{
		Register(dashboard);
		Register(inventory);
		Register(sales);
		Register(expenses);
		Register(reports);

		var grid = new Grid
		{
			RowDefinitions =
			{
				new RowDefinition { Height = GridLength.Star },
				new RowDefinition { Height = GridLength.Auto },
			}
		};
		Grid.SetRow(_sectionHost, 0);
		Grid.SetRow(_bar, 1);
		grid.Add(_sectionHost);
		grid.Add(_bar);
		Content = grid;

		_bar.SectionSelected += SelectSection;

		SelectSection("dashboard");
	}

	private void Register(View view)
	{
		if (view is ISectionView s)
			_sections[s.SectionId] = view;
	}

	public void SelectSection(string id)
	{
		if (!_sections.TryGetValue(id, out var view))
			return;

		CurrentSection = id;
		_sectionHost.Content = view;
		_bar.Active = id;

		if (view is ISectionView s)
		{
			Title = s.SectionTitle;
			ToolbarItems.Clear();
			foreach (var a in s.Actions)
				ToolbarItems.Add(new ToolbarItem { Text = a.Text, Command = a.Command });
		}

		SectionChanged?.Invoke(id);
	}
}
