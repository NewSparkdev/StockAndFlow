using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class SalesPage : ContentPage
{
	public SalesPage(SalesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
