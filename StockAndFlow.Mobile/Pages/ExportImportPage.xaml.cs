using System.IO;
using StockAndFlow.Services;

namespace StockAndFlow.Mobile.Pages;

public partial class ExportImportPage : ContentPage
{
	private readonly ExportImportService _service;

	public ExportImportPage(ExportImportService service)
	{
		InitializeComponent();
		_service = service;
	}

	private void SetBusy(bool busy)
	{
		Busy.IsRunning = busy;
		Busy.IsVisible = busy;
		ExportButton.IsEnabled = !busy;
		ImportButton.IsEnabled = !busy;
	}

	private async void OnExportClicked(object? sender, EventArgs e)
	{
		try
		{
			SetBusy(true);
			var path = Path.Combine(FileSystem.CacheDirectory,
				$"StockAndFlow_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
			await _service.ExportToExcelAsync(path);
			await Share.Default.RequestAsync(new ShareFileRequest
			{
				Title = "Stock & Flow Export",
				File = new ShareFile(path)
			});
		}
		catch (Exception ex)
		{
			await DisplayAlert("Export failed", ex.Message, "OK");
		}
		finally
		{
			SetBusy(false);
		}
	}

	private async void OnImportClicked(object? sender, EventArgs e)
	{
		try
		{
			var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select an exported .xlsx file" });
			if (file == null)
				return;

			SetBusy(true);
			var result = await _service.ImportFromExcelAsync(file.FullPath);

			var summary =
				$"Inventory: +{result.InventoryAdded} added, {result.InventoryUpdated} updated\n" +
				$"Sales: +{result.SalesAdded} added, {result.SalesUpdated} updated\n" +
				$"Expenses: +{result.ExpensesAdded} added, {result.ExpensesUpdated} updated";

			await DisplayAlert(result.Success ? "Import complete" : "Import finished with issues", summary, "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlert("Import failed", ex.Message, "OK");
		}
		finally
		{
			SetBusy(false);
		}
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}
}
