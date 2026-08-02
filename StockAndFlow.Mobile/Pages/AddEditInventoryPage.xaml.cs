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
		MakeBarcodeButton.Clicked += OnMakeBarcodeClicked;
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

	private async void OnMakeBarcodeClicked(object? sender, EventArgs e)
	{
		try
		{
			var barcode = new StockAndFlow.Services.BarcodeService();

			// No SKU yet? Make one and put it in the field so the label and the item agree.
			if (string.IsNullOrWhiteSpace(_viewModel.Sku))
			{
				// Pass the codes already in use so the new one can't collide.
				_viewModel.Sku = barcode.GenerateSku(_viewModel.ExistingSkus);
				await DisplayAlert("Barcode created",
					$"This item had no code, so we made one: {_viewModel.Sku}\n\n" +
					"Remember to save the item so the code sticks.",
					"OK");
			}

			var code = _viewModel.Sku!.Trim();

			// CODE_128 is the scanner-friendly default; fall back to QR for anything with
			// characters it can't carry (accents, symbols).
			var symbology = barcode.CanEncode(code, StockAndFlow.Services.BarcodeSymbology.Code128)
				? StockAndFlow.Services.BarcodeSymbology.Code128
				: StockAndFlow.Services.BarcodeSymbology.QrCode;

			var png = barcode.CreateLabelPng(code, symbology,
				caption: string.IsNullOrWhiteSpace(_viewModel.Name) ? null : _viewModel.Name,
				scale: 6);

			var safeName = string.Join("_", code.Split(Path.GetInvalidFileNameChars()));
			var file = Path.Combine(FileSystem.CacheDirectory, $"barcode_{safeName}.png");
			await File.WriteAllBytesAsync(file, png);

			await Share.Default.RequestAsync(new ShareFileRequest
			{
				Title = "Barcode label",
				File = new ShareFile(file)
			});
		}
		catch (Exception ex)
		{
			await DisplayAlert("Couldn't create barcode", ex.Message, "OK");
		}
	}
}
