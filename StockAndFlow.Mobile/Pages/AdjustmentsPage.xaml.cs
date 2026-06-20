using StockAndFlow.Models;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class AdjustmentsPage : ContentPage
{
	private readonly AdjustmentsViewModel _viewModel;

	public AdjustmentsPage(AdjustmentsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_viewModel = viewModel;
	}

	private void Select(object sender)
	{
		if (sender is BindableObject b && b.BindingContext is InventoryAdjustment item)
			_viewModel.SelectedAdjustment = item;
	}

	private void OnRowTapped(object sender, TappedEventArgs e)
	{
		Select(sender);
		_viewModel.ViewDetailsCommand.Execute(null);
	}

	private void OnDeleteSwipe(object sender, EventArgs e)
	{
		Select(sender);
		_viewModel.DeleteAdjustmentCommand.Execute(null);
	}
}
