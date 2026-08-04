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
			var fileName = $"StockAndFlow_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
			var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);
			await _service.ExportToExcelAsync(cachePath);

			// Save a copy to a predictable Downloads/Stock & Flow folder so it's easy to find
			// again when importing. Returns null on platforms without a public Downloads folder.
			var savedLocation = TrySaveToDownloads(cachePath, fileName);

			if (savedLocation != null)
			{
				var share = await DisplayAlert(
					"Export saved",
					$"Saved to {savedLocation}\n\nYou can find it there when importing, or share a copy now.",
					"Share", "Done");
				if (share)
					await ShareFileAsync(cachePath);
			}
			else
			{
				// No public Downloads folder (e.g. iOS) — fall back to the share sheet.
				await ShareFileAsync(cachePath);
			}
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

	private static Task ShareFileAsync(string path) =>
		Share.Default.RequestAsync(new ShareFileRequest
		{
			Title = "Stock & Flow Export",
			File = new ShareFile(path)
		});

	/// <summary>
	/// Copies the exported file into the platform's public Downloads folder (under a "Stock and Flow"
	/// subfolder) and returns a user-facing location string, or null if that isn't supported.
	/// </summary>
	private static string? TrySaveToDownloads(string sourcePath, string fileName)
	{
#if ANDROID
		// Android 10+ (API 29): write into the public Downloads collection via MediaStore — no
		// storage permission required, and the file is visible in the Files app under Downloads.
		if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Q)
			return null;

		var resolver = Android.App.Application.Context.ContentResolver!;
		var values = new Android.Content.ContentValues();
		values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
		values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, $"{Android.OS.Environment.DirectoryDownloads}/Stock & Flow");

		var uri = resolver.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri!, values);
		if (uri == null)
			return null;

		using (var dest = resolver.OpenOutputStream(uri)!)
		using (var src = File.OpenRead(sourcePath))
			src.CopyTo(dest);

		return $"Downloads › Stock & Flow › {fileName}";
#elif WINDOWS
		var folder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Stock & Flow");
		Directory.CreateDirectory(folder);
		File.Copy(sourcePath, Path.Combine(folder, fileName), overwrite: true);
		return $"Downloads\\Stock & Flow\\{fileName}";
#else
		return null;
#endif
	}

	private async void OnImportClicked(object? sender, EventArgs e)
	{
		try
		{
			// Filter to Excel files so the picker opens a proper, navigable document browser
			// (rather than a dead-end empty "Recent" view) that returns to the app on cancel.
			var xlsxType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
			{
				[DevicePlatform.Android] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
				[DevicePlatform.iOS] = new[] { "org.openxmlformats.spreadsheetml.sheet" },
				[DevicePlatform.MacCatalyst] = new[] { "xlsx" },
				[DevicePlatform.WinUI] = new[] { ".xlsx" },
			});

			var file = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Select an exported .xlsx file",
				FileTypes = xlsxType
			});
			if (file == null)
				return; // user cancelled — stay on this page

			SetBusy(true);
			var result = await _service.ImportFromExcelAsync(file.FullPath);

			// Out of this month's imports — offer the upgrade rather than reporting a failure.
			if (result.BlockedByEntitlement is { } blocked)
			{
				var services = IPlatformApplication.Current!.Services;
				await services.GetRequiredService<StockAndFlow.Services.EntitlementService>()
					.OfferUpgradeAsync(blocked,
						services.GetRequiredService<StockAndFlow.Platform.IDialogService>(),
						services.GetRequiredService<StockAndFlow.Platform.IPaywallPresenter>());
				return;
			}

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
