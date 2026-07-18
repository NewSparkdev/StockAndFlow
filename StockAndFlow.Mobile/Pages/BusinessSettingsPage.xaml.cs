using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class BusinessSettingsPage : ContentPage
{
	public BusinessSettingsPage(BusinessSettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		// Pushed onto the Settings nav stack, so Save/Cancel pop back to the Settings menu.
		viewModel.CloseRequested += async (_, _) => await Navigation.PopAsync();
	}
}
