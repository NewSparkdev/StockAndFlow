using Microsoft.Maui.Controls.Shapes;

namespace StockAndFlow.Mobile.Controls;

/// <summary>
/// Custom bottom navigation bar: a row of five big rounded tile-buttons, each with its icon and
/// label together on the tile. Replaces Shell's native tab bar (which can't render labeled tiles).
/// Set <see cref="Active"/> per page to highlight the current tab; tapping a tile navigates.
/// </summary>
public class BottomNavBar : ContentView
{
	public static readonly BindableProperty ActiveProperty = BindableProperty.Create(
		nameof(Active), typeof(string), typeof(BottomNavBar), default(string),
		propertyChanged: (b, _, _) => ((BottomNavBar)b).UpdateSelection());

	/// <summary>Id of the current tab (e.g. "dashboard") so its tile shows selected.</summary>
	public string Active
	{
		get => (string)GetValue(ActiveProperty);
		set => SetValue(ActiveProperty, value);
	}

	/// <summary>Raised when a tile is tapped. The host swaps the section (no page recreation).</summary>
	public event Action<string>? SectionSelected;

	private static readonly (string Id, string Label, string Icon)[] Tabs =
	{
		("dashboard", "Dashboard", "tab_dashboard.png"),
		("inventory", "Inventory", "tab_inventory.png"),
		("sales", "Sales", "tab_sales.png"),
		("expenses", "Expenses", "tab_expenses.png"),
		("reports", "Reports", "tab_reports.png"),
	};

	private readonly Dictionary<string, Border> _tiles = new();

	public BottomNavBar()
	{
		var grid = new Grid { ColumnSpacing = 6, Padding = new Thickness(8, 8, 8, 10) };
		foreach (var _ in Tabs)
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

		for (var i = 0; i < Tabs.Length; i++)
		{
			var tab = Tabs[i];
			var tile = BuildTile(tab.Icon, tab.Label);
			_tiles[tab.Id] = tile;
			Grid.SetColumn(tile, i);

			var id = tab.Id;
			tile.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(() =>
				{
					if (Active == id)
						return;
					Active = id;
					SectionSelected?.Invoke(id);
				})
			});
			grid.Add(tile);
		}

		// Fixed height so the bar measures identically on every page — otherwise the Auto grid row
		// can resolve to slightly different heights per page, making the whole bar jump on navigation.
		HeightRequest = 84;
		BackgroundColor = Color.FromArgb("#512BD4");
		Content = grid;
		UpdateSelection();
	}

	private static Border BuildTile(string icon, string label) => new()
	{
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = 16 },
		Padding = new Thickness(0, 9),
		Content = new VerticalStackLayout
		{
			Spacing = 4,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Image { Source = icon, WidthRequest = 26, HeightRequest = 26, HorizontalOptions = LayoutOptions.Center },
				new Label { Text = label, TextColor = Colors.White, FontSize = 11, HorizontalTextAlignment = TextAlignment.Center }
			}
		}
	};

	private void UpdateSelection()
	{
		foreach (var (id, tile) in _tiles)
		{
			var selected = id == Active;
			tile.BackgroundColor = selected ? Color.FromArgb("#33FFFFFF") : Colors.Transparent;
			tile.Opacity = selected ? 1.0 : 0.72;
			if (tile.Content is VerticalStackLayout stack && stack.Children.Count > 1 && stack.Children[1] is Label lbl)
				lbl.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
		}
	}
}
