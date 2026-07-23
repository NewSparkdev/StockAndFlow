using System.Linq;
using System.Windows;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.Views.Dialogs
{
    public partial class InventoryDetailsDialog : Window
    {
        private readonly BomService? _bomService;
        private readonly InventoryService? _inventoryService;

        public InventoryDetailsDialog()
        {
            InitializeComponent();
        }

        public InventoryDetailsDialog(InventoryItem item, BomService bomService, InventoryService inventoryService)
            : this()
        {
            DataContext = item;
            _bomService = bomService;
            _inventoryService = inventoryService;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_bomService == null || _inventoryService == null || DataContext is not InventoryItem item)
                return;

            var components = await _bomService.GetComponentsForItemAsync(item.Id);
            if (components.Count == 0) return;

            var all = await _inventoryService.GetAllItemsAsync();
            var rows = components
                .Select(c => new BomDisplayRow(
                    all.FirstOrDefault(i => i.Id == c.ComponentItemId)?.Name ?? "Unknown",
                    $"× {c.QuantityPerUnit:G}"))
                .Where(r => r.Name != "Unknown")
                .ToList();

            if (rows.Count > 0)
            {
                BomItemsList.ItemsSource = rows;
                BomSection.Visibility = Visibility.Visible;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private sealed record BomDisplayRow(string Name, string QuantityDisplay);
    }
}
