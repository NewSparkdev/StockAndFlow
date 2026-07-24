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

	// Picker.SelectedItem two-way binding is unreliable on iOS when ItemsSource loads
	// asynchronously. Drive PendingComponent directly from the index instead.
	private void OnComponentPickerChanged(object? sender, EventArgs e)
	{
		var idx = ComponentPicker.SelectedIndex;
		_viewModel.PendingComponent = idx >= 0 ? _viewModel.AvailableComponents[idx] : null;
	}

	private void OnAddBomComponentClicked(object? sender, EventArgs e)
	{
		if (decimal.TryParse(PendingQtyEntry.Text, out var qty) && qty > 0)
			_viewModel.PendingQty = qty;
		_viewModel.AddBomComponent();
		ComponentPicker.SelectedIndex = -1;
		PendingQtyEntry.Text = "1";
	}

	private async void OnScanSkuClicked(object? sender, EventArgs e)
	{
		var scanPage = new BarcodeScanPage();
		scanPage.BarcodeDetected += (_, barcode) => _viewModel.Sku = barcode;
		await Navigation.PushAsync(scanPage);
	}
}
