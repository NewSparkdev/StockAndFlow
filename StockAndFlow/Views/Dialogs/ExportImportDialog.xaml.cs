using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using StockAndFlow.Services;

namespace StockAndFlow.Views.Dialogs
{
    public partial class ExportImportDialog : Window
    {
        private readonly ExportImportService _exportImportService;

        public ExportImportDialog(ExportImportService exportImportService)
        {
            InitializeComponent();
            _exportImportService = exportImportService;
        }

        private async void ExportOption_Click(object sender, MouseButtonEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"StockAndFlow_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    await _exportImportService.ExportToExcelAsync(saveDialog.FileName);

                    Mouse.OverrideCursor = null;

                    MessageBox.Show(
                        $"Data exported successfully!\n\nFile saved to:\n{saveDialog.FileName}\n\nYou can now edit this file offline and import it back later.",
                        "Export Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;

                    MessageBox.Show(
                        $"Export failed: {ex.Message}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private async void ImportOption_Click(object sender, MouseButtonEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    var result = await _exportImportService.ImportFromExcelAsync(openDialog.FileName);

                    Mouse.OverrideCursor = null;

                    if (result.Success)
                    {
                        var message = result.GetSummary();

                        if (result.HasDuplicateWarning)
                        {
                            var warningResult = MessageBox.Show(
                                $"{result.DuplicateMessage}\n\nDo you want to continue with the import?\n\nThis will add any new records found in the file.",
                                "Duplicate Import Warning",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (warningResult == MessageBoxResult.No)
                            {
                                Close();
                                return;
                            }
                        }

                        MessageBox.Show(
                            message,
                            "Import Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            result.ErrorMessage ?? "Import failed",
                            "Import Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;

                    MessageBox.Show(
                        $"Import failed: {ex.Message}",
                        "Import Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
