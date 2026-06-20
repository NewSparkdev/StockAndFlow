using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly IDialogService _dialogService;
        private readonly IEditorPresenter _editorPresenter;

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

        public InventoryViewModel(InventoryService inventoryService, IDialogService dialogService, IEditorPresenter editorPresenter)
        {
            _inventoryService = inventoryService;
            _dialogService = dialogService;
            _editorPresenter = editorPresenter;

            AddItemCommand = new RelayCommand(async () => await AddItemAsync());
            EditItemCommand = new RelayCommand(async () => await EditItemAsync(), () => SelectedItem != null);
            DeleteItemCommand = new RelayCommand(async () => await DeleteItemAsync(), () => SelectedItem != null);
            ViewDetailsCommand = new RelayCommand(async () => await ViewDetailsAsync(), () => SelectedItem != null);
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

        private async Task AddItemAsync()
        {
            await _editorPresenter.ShowAddInventoryAsync();
        }

        private async Task EditItemAsync()
        {
            if (SelectedItem == null)
                return;

            await _editorPresenter.ShowEditInventoryAsync(SelectedItem);
        }

        private async Task ViewDetailsAsync()
        {
            if (SelectedItem == null)
                return;

            await _editorPresenter.ShowInventoryDetailsAsync(SelectedItem);
        }

        private async Task DeleteItemAsync()
        {
            if (SelectedItem == null)
                return;

            if (await _dialogService.ShowConfirmAsync(
                "Confirm Delete",
                $"Are you sure you want to delete '{SelectedItem.Name}'?\n\nThis action cannot be undone."))
            {
                await _inventoryService.DeleteItemAsync(SelectedItem.Id);
                SelectedItem = null;
            }
        }
    }
}
