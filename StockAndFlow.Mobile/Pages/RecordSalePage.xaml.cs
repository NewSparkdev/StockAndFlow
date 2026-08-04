using System.IO;
using StockAndFlow.Mobile.Services;
using StockAndFlow.Models;
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
		ScanItemButton.Clicked += OnScanItemClicked;

		viewModel.CloseRequested += async (_, _) => await CloseAsync();
		viewModel.SaleCompleted += async (_, transaction) =>
		{
			await DisplayAlert("Sale recorded",
				$"Recorded {transaction.ItemCount} item(s) totalling {transaction.Revenue:C2}.", "OK");
			await OfferInvoiceAsync(transaction);
			await CloseAsync();
		};
		viewModel.SaleFailed += async (_, message) => await DisplayAlert("Sale not completed", message, "OK");
	}

	private async Task OfferInvoiceAsync(SaleTransaction transaction)
	{
		if (!await DisplayAlert("Invoice", "Generate a PDF invoice for this sale?", "Yes", "No"))
			return;

		if (!await _viewModel.IsBusinessConfiguredAsync())
		{
			await DisplayAlert("Business details needed",
				"Add your business name and details in Settings before generating invoices.", "OK");
			return;
		}

		// Free tier: 5 invoices per month, resets monthly (the sale itself is already recorded
		// and is never gated — only the PDF nicety is metered).
		var entitlements = IPlatformApplication.Current!.Services.GetRequiredService<EntitlementService>();
		if (!await entitlements.TryConsumeMonthlyAllowanceAsync(
			EntitlementService.InvoiceQuota, EntitlementService.FreeMaxInvoicesPerMonth))
		{
			await EntitlementService.ShowPaywallAsync(
				$"The free plan includes {EntitlementService.FreeMaxInvoicesPerMonth} PDF invoices per month — you've used them for now. They reset next month, or Pro makes them unlimited.");
			return;
		}

		try
		{
			var fileName = $"Invoice_{transaction.TransactionId.ToString()[..8].ToUpperInvariant()}.pdf";
			var outputPath = Path.Combine(FileSystem.CacheDirectory, fileName);
			var generated = await _viewModel.GenerateInvoiceAsync(transaction, outputPath);
			if (generated != null && File.Exists(generated))
			{
				await Share.Default.RequestAsync(new ShareFileRequest
				{
					Title = "Invoice",
					File = new ShareFile(generated)
				});
			}
			else
			{
				await DisplayAlert("Invoice", "The invoice could not be generated.", "OK");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Invoice failed", ex.Message, "OK");
		}
	}

	private async void OnScanItemClicked(object? sender, EventArgs e)
	{
		var scanPage = new BarcodeScanPage();
		scanPage.BarcodeDetected += (_, barcode) => _viewModel.SelectItemBySku(barcode);
		await Navigation.PushAsync(scanPage);
	}

	private async Task CloseAsync()
	{
		if (_closing) return;
		_closing = true;
		await Navigation.PopModalAsync();
	}
}
