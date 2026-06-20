using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class RecordSalePage : ContentPage
{
	private readonly RecordSaleViewModel _viewModel;
	private bool _closing;

	public RecordSalePage(RecordSaleViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;

		viewModel.CloseRequested += async (_, _) => await CloseAsync();
		viewModel.SaleCompleted += async (_, transaction) =>
		{
			await DisplayAlert("Sale recorded",
				$"Recorded {transaction.ItemCount} item(s) totalling {transaction.Revenue:C2}.", "OK");
			await CloseAsync();
		};
		viewModel.SaleFailed += async (_, message) => await DisplayAlert("Sale not completed", message, "OK");
	}

	private async Task CloseAsync()
	{
		if (_closing) return;
		_closing = true;
		await Navigation.PopModalAsync();
	}
}
