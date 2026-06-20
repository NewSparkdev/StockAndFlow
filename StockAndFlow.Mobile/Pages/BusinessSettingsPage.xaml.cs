using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class BusinessSettingsPage : ContentPage
{
	public BusinessSettingsPage(BusinessSettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
