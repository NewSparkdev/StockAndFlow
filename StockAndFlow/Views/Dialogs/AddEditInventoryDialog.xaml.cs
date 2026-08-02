using System;
using System.IO;
using System.Windows;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class AddEditInventoryDialog : Window
    {
        public AddEditInventoryDialog()
        {
            InitializeComponent();
        }

        public AddEditInventoryDialog(AddEditInventoryViewModel viewModel) : this()
        {
            DataContext = viewModel;

            viewModel.CloseRequested += (sender, result) =>
            {
                DialogResult = result;
                Close();
            };

            Loaded += async (_, _) => await viewModel.InitializeAsync();
        }

        private void OnMakeBarcodeClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AddEditInventoryViewModel vm)
                return;

            try
            {
                var barcode = new BarcodeService();

                // No SKU yet? Make one and put it in the field so label and item agree.
                if (string.IsNullOrWhiteSpace(vm.Sku))
                {
                    vm.Sku = barcode.GenerateSku();
                    MessageBox.Show(
                        $"This item had no code, so we made one: {vm.Sku}\n\nRemember to save the item so the code sticks.",
                        "Barcode created", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                var code = vm.Sku!.Trim();

                // CODE_128 is the scanner-friendly default; QR carries anything it can't.
                var symbology = barcode.CanEncode(code, BarcodeSymbology.Code128)
                    ? BarcodeSymbology.Code128
                    : BarcodeSymbology.QrCode;

                var png = barcode.CreateLabelPng(code, symbology,
                    caption: string.IsNullOrWhiteSpace(vm.Name) ? null : vm.Name,
                    scale: 6);

                var downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloads);

                var safeName = string.Join("_", code.Split(Path.GetInvalidFileNameChars()));
                var path = Path.Combine(downloads, $"barcode_{safeName}.png");
                File.WriteAllBytes(path, png);

                if (MessageBox.Show($"Saved to:\n{path}\n\nOpen it now?", "Barcode label",
                        MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                    {
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Couldn't create barcode",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
