using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class EditSalePage : ContentPage
{
	public EditSalePage(EditSaleViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
