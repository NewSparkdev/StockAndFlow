using StockAndFlow.Models;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class InventoryPage : ContentPage
{
	private readonly InventoryViewModel _viewModel;

	public InventoryPage(InventoryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_viewModel = viewModel;
	}

	private void Select(object sender)
	{
		if (sender is BindableObject b && b.BindingContext is InventoryItem item)
			_viewModel.SelectedItem = item;
	}

	private void OnRowTapped(object sender, TappedEventArgs e)
	{
		Select(sender);
		_viewModel.ViewDetailsCommand.Execute(null);
	}

	private void OnEditSwipe(object sender, EventArgs e)
	{
		Select(sender);
		_viewModel.EditItemCommand.Execute(null);
	}

	private void OnDeleteSwipe(object sender, EventArgs e)
	{
		Select(sender);
		_viewModel.DeleteItemCommand.Execute(null);
	}
}
