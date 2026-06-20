using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class AdjustmentDetailsPage : ContentPage
{
	public AdjustmentDetailsPage(AdjustmentDetailsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
