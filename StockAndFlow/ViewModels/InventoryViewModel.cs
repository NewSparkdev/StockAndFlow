using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;

        private ObservableCollection<InventoryItem> _items = new();
        private InventoryItem? _selectedItem;
        private string _searchText = string.Empty;

        public ObservableCollection<InventoryItem> Items
        {
            get => _items;
            set
            {
                if (SetProperty(ref _items, value))
                {
                    OnPropertyChanged(nameof(LowStockItems));
                    OnPropertyChanged(nameof(LowStockCount));
                    OnPropertyChanged(nameof(HasLowStock));
                }
            }
        }

        public ObservableCollection<InventoryItem> LowStockItems
        {
            get
            {
                var lowStockItems = Items.Where(i => i.IsLowStock || i.IsOutOfStock)
                                        .OrderBy(i => i.QuantityOnHand)
                                        .ThenBy(i => i.Name)
                                        .ToList();
                return new ObservableCollection<InventoryItem>(lowStockItems);
            }
        }

        public int LowStockCount => Items.Count(i => i.IsLowStock || i.IsOutOfStock);

        public bool HasLowStock => LowStockCount > 0;

        public InventoryItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = SearchAsync();
                }
            }
        }


        public ICommand AddItemCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand RefreshCommand { get; }

        public InventoryViewModel(InventoryService inventoryService)
        {
            _inventoryService = inventoryService;

            AddItemCommand = new RelayCommand(AddItem);
            EditItemCommand = new RelayCommand(EditItem, () => SelectedItem != null);
            DeleteItemCommand = new RelayCommand(async () => await DeleteItemAsync(), () => SelectedItem != null);
            ViewDetailsCommand = new RelayCommand(ViewDetails, () => SelectedItem != null);
            RefreshCommand = new RelayCommand(async () => await LoadItemsAsync());

            // Subscribe to inventory changes
            _inventoryService.InventoryChanged += (s, e) => _ = LoadItemsAsync();

            // Initial load
            _ = LoadItemsAsync();
        }

        private async Task LoadItemsAsync()
        {
            var items = await _inventoryService.GetAllItemsAsync();
            Items = new ObservableCollection<InventoryItem>(items.OrderBy(i => i.Name));
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadItemsAsync();
                return;
            }

            var results = await _inventoryService.SearchAsync(SearchText);
            Items = new ObservableCollection<InventoryItem>(results.OrderBy(i => i.Name));
        }

        private void AddItem()
        {
            var viewModel = new AddEditInventoryViewModel(_inventoryService);
            var dialog = new Views.Dialogs.AddEditInventoryDialog(viewModel);
            dialog.ShowDialog();
        }

        private void EditItem()
        {
            if (SelectedItem == null)
                return;

            var viewModel = new AddEditInventoryViewModel(_inventoryService, SelectedItem);
            var dialog = new Views.Dialogs.AddEditInventoryDialog(viewModel);
            dialog.ShowDialog();
        }

        private void ViewDetails()
        {
            if (SelectedItem == null)
                return;

            var dialog = new Views.Dialogs.InventoryDetailsDialog
            {
                DataContext = SelectedItem
            };
            dialog.ShowDialog();
        }

        private async Task DeleteItemAsync()
        {
            if (SelectedItem == null)
                return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{SelectedItem.Name}'?\n\nThis action cannot be undone.",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _inventoryService.DeleteItemAsync(SelectedItem.Id);
                SelectedItem = null;
            }
        }
    }
}
