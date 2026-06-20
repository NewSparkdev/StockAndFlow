using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class SaleDetailsPage : ContentPage
{
	public SaleDetailsPage(SaleDetailsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
