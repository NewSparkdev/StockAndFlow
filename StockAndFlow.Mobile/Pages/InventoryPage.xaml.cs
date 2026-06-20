using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class InventoryPage : ContentPage
{
	public InventoryPage(InventoryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
