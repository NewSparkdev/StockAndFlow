using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class AddEditInventoryPage : ContentPage
{
	private readonly AddEditInventoryViewModel _viewModel;

	public AddEditInventoryPage(AddEditInventoryViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
		ScanSkuButton.Clicked += OnScanSkuClicked;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.InitializeAsync();
	}

	private async void OnScanSkuClicked(object? sender, EventArgs e)
	{
		var scanPage = new BarcodeScanPage();
		scanPage.BarcodeDetected += (_, barcode) => _viewModel.Sku = barcode;
		await Navigation.PushAsync(scanPage);
	}
}
