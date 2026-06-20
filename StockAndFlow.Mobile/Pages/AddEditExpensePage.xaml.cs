using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class AddEditExpensePage : ContentPage
{
	public AddEditExpensePage(AddEditExpenseViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}
}
