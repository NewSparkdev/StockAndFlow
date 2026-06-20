using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class ReportsPage : ContentPage
{
	public ReportsPage(ReportsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
