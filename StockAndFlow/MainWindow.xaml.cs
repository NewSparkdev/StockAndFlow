using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StockAndFlow;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnPrintLabelsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var inventory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<StockAndFlow.Services.InventoryService>(App.Services);

            var items = (await inventory.GetAllItemsAsync())
                .Where(i => !string.IsNullOrWhiteSpace(i.Sku))
                .OrderBy(i => i.Name)
                .ToList();

            if (items.Count == 0)
            {
                MessageBox.Show(
                    "No products have a barcode yet.\n\nOpen an item and click \"Create barcode label\" to give it one, then come back here to print a sheet.",
                    "Nothing to print", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var barcodes = new StockAndFlow.Services.BarcodeService();
            var requests = items.Select(i =>
                new StockAndFlow.Services.BarcodeService.LabelRequest(i.Sku!.Trim(), i.Name)).ToList();
            var pdf = barcodes.CreateLabelSheetPdf(requests);

            var downloads = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            System.IO.Directory.CreateDirectory(downloads);
            var path = System.IO.Path.Combine(downloads, $"barcode_labels_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            System.IO.File.WriteAllBytes(path, pdf);

            var answer = MessageBox.Show(
                $"Created labels for {items.Count} product(s):\n{path}\n\n" +
                "When printing, choose 100% / Actual size — \"fit to page\" shrinks the bars and they may stop scanning.\n\nOpen it now?",
                "Barcode labels", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Couldn't create labels", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnShopifySettingsClick(object sender, RoutedEventArgs e)
    {
        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<StockAndFlow.ViewModels.ShopifySettingsViewModel>(App.Services);
        var dialog = new Views.Dialogs.ShopifySettingsDialog(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    /// <summary>
    /// Handles mouse wheel scrolling in the Reports tab to prevent child controls
    /// (DataGrids, Charts) from capturing scroll events
    /// </summary>
    private void ReportsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // Calculate new scroll offset
            double newOffset = scrollViewer.VerticalOffset - (e.Delta / 3.0);

            // Clamp to valid range
            newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));

            // Apply scroll
            scrollViewer.ScrollToVerticalOffset(newOffset);

            // Mark event as handled to prevent child controls from capturing it
            e.Handled = true;
        }
    }
}