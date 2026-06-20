using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class ExpenseDetailsPage : ContentPage
{
	public ExpenseDetailsPage(ExpenseDetailsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
