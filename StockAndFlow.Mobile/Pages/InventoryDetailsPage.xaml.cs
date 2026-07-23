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
		var rows = components
			.Select(c => new BomDisplayRow(
				all.FirstOrDefault(i => i.Id == c.ComponentItemId)?.Name ?? "Unknown",
				$"× {c.QuantityPerUnit:G}"))
			.Where(r => r.Name != "Unknown")
			.ToList();

		if (rows.Count > 0)
		{
			BomList.ItemsSource = rows;
			BomSection.IsVisible = true;
		}
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

	private sealed record BomDisplayRow(string Name, string QuantityDisplay);
}
