using StockAndFlow.Models;
using StockAndFlow.Platform;

namespace StockAndFlow.Mobile.Pages;

public partial class InventoryDetailsPage : ContentPage
{
	private readonly InventoryItem _item;
	private readonly IEditorPresenter _editorPresenter;

	public InventoryDetailsPage(InventoryItem item, IEditorPresenter editorPresenter)
	{
		InitializeComponent();
		_item = item;
		_editorPresenter = editorPresenter;
		BindingContext = item;
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
	}
}
