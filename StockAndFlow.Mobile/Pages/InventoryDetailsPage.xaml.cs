using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile.Pages;

public partial class InventoryDetailsPage : ContentPage
{
	private readonly InventoryItem _item;
	private readonly IEditorPresenter _editorPresenter;
	private readonly BomService _bomService;
	private readonly InventoryService _inventoryService;
	private bool _bomLoaded;

	public InventoryDetailsPage(
		InventoryItem item,
		IEditorPresenter editorPresenter,
		BomService bomService,
		InventoryService inventoryService)
	{
		InitializeComponent();
		_item = item;
		_editorPresenter = editorPresenter;
		_bomService = bomService;
		_inventoryService = inventoryService;
		BindingContext = item;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (_bomLoaded) return;
		_bomLoaded = true;

		var components = await _bomService.GetComponentsForItemAsync(_item.Id);
		if (components.Count == 0) return;

		var all = await _inventoryService.GetAllItemsAsync();

		// Build rows on background thread, then marshal all UI mutations to main thread.
		bool isDark = Application.Current?.PlatformAppTheme == AppTheme.Dark;
		var qtyColor = Color.FromArgb(isDark ? "#9FA8DA" : "#3949AB");

		var rows = components
			.Select(c => (name: all.FirstOrDefault(i => i.Id == c.ComponentItemId)?.Name,
			              qty: $"× {c.QuantityPerUnit:G}"))
			.Where(r => r.name != null)
			.ToList();

		if (rows.Count == 0) return;

		MainThread.BeginInvokeOnMainThread(() =>
		{
			foreach (var (name, qty) in rows)
			{
				var row = new Grid
				{
					ColumnDefinitions =
					{
						new ColumnDefinition(GridLength.Star),
						new ColumnDefinition(GridLength.Auto)
					},
					Padding = new Thickness(4, 6)
				};
				row.Add(new Label { Text = name, VerticalOptions = LayoutOptions.Center }, 0, 0);
				row.Add(new Label
				{
					Text = qty,
					TextColor = qtyColor,
					FontAttributes = FontAttributes.Bold,
					VerticalOptions = LayoutOptions.Center
				}, 1, 0);
				BomList.Add(row);
			}
			BomSection.IsVisible = true;
		});
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}

	private async void OnEditClicked(object? sender, EventArgs e)
	{
		await _editorPresenter.ShowEditInventoryAsync(_item);
		// Item was mutated in-place by the edit form; reset binding to refresh all labels.
		BindingContext = null;
		BindingContext = _item;
		_bomLoaded = false;
	}

}
