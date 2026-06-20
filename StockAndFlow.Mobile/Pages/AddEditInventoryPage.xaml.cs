using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class AddEditInventoryPage : ContentPage
{
	public AddEditInventoryPage(AddEditInventoryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
