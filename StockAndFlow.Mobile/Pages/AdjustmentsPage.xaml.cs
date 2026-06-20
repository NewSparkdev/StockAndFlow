using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class AdjustmentsPage : ContentPage
{
	public AdjustmentsPage(AdjustmentsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
