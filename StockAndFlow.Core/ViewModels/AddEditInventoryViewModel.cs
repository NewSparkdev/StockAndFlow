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
    public class AddEditInventoryViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly BomService _bomService;
        private readonly IFilePickerService _filePicker;
        private readonly IDialogService _dialogService;
        private readonly InventoryItem _originalItem;
        private readonly bool _isEditMode;
        private bool _initialized;

        private string _name = string.Empty;
        private string? _sku;
        private string? _category;
        private decimal _costPerUnit;
        private decimal _salePrice;
        private int _quantityOnHand;
        private int _minimumStockLevel;
        private string? _supplier;
        private string? _notes;
        private string _imagePath = string.Empty;

        private InventoryItem? _pendingComponent;
        private decimal _pendingQty = 1m;

        public event EventHandler<bool>? CloseRequested;

        public string DialogTitle => _isEditMode ? "Edit Inventory Item" : "Add New Inventory Item";

        public ObservableCollection<BomComponentEntry> BomComponents { get; } = new();
        public ObservableCollection<InventoryItem> AvailableComponents { get; } = new();

        public InventoryItem? PendingComponent
        {
            get => _pendingComponent;
            set
            {
                if (SetProperty(ref _pendingComponent, value))
                    OnPropertyChanged(nameof(CanAddComponent));
            }
        }

        public decimal PendingQty
        {
            get => _pendingQty;
            set
            {
                if (SetProperty(ref _pendingQty, value))
                    OnPropertyChanged(nameof(CanAddComponent));
            }
        }

        public bool CanAddComponent => PendingComponent != null && PendingQty > 0;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                    OnPropertyChanged(nameof(IsValid));
            }
        }

        public string? Sku
        {
            get => _sku;
            set => SetProperty(ref _sku, value);
        }

        public string? Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public decimal CostPerUnit
        {
            get => _costPerUnit;
            set
            {
                if (SetProperty(ref _costPerUnit, value))
                {
                    OnPropertyChanged(nameof(ProfitMarginPerUnit));
                    OnPropertyChanged(nameof(ProfitMarginPercentage));
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public decimal SalePrice
        {
            get => _salePrice;
            set
            {
                if (SetProperty(ref _salePrice, value))
                {
                    OnPropertyChanged(nameof(ProfitMarginPerUnit));
                    OnPropertyChanged(nameof(ProfitMarginPercentage));
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public int QuantityOnHand
        {
            get => _quantityOnHand;
            set
            {
                if (SetProperty(ref _quantityOnHand, value))
                    OnPropertyChanged(nameof(IsValid));
            }
        }

        public int MinimumStockLevel
        {
            get => _minimumStockLevel;
            set => SetProperty(ref _minimumStockLevel, value);
        }

        public string? Supplier
        {
            get => _supplier;
            set => SetProperty(ref _supplier, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                if (SetProperty(ref _imagePath, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasImage));
            }
        }

        public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);

        public decimal ProfitMarginPerUnit => SalePrice - CostPerUnit;

        public decimal ProfitMarginPercentage => SalePrice > 0
            ? (ProfitMarginPerUnit / SalePrice) * 100
            : 0;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Name) &&
            CostPerUnit >= 0 &&
            SalePrice >= 0 &&
            QuantityOnHand >= 0;

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand AddBomComponentCommand { get; }

        // Constructor for adding a new item
        public AddEditInventoryViewModel(
            InventoryService inventoryService,
            BomService bomService,
            IFilePickerService filePicker,
            IDialogService dialogService)
        {
            _inventoryService = inventoryService;
            _bomService = bomService;
            _filePicker = filePicker;
            _dialogService = dialogService;
            _originalItem = new InventoryItem();
            _isEditMode = false;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsValid);
            CancelCommand = new RelayCommand(Cancel);
            BrowseImageCommand = new RelayCommand(async () => await BrowseImageAsync());
            AddBomComponentCommand = new RelayCommand(AddBomComponent, () => CanAddComponent);
        }

        // Constructor for editing an existing item
        public AddEditInventoryViewModel(
            InventoryService inventoryService,
            BomService bomService,
            InventoryItem item,
            IFilePickerService filePicker,
            IDialogService dialogService)
        {
            _inventoryService = inventoryService;
            _bomService = bomService;
            _filePicker = filePicker;
            _dialogService = dialogService;
            _originalItem = item;
            _isEditMode = true;

            Name = item.Name;
            Sku = item.Sku;
            Category = item.Category;
            CostPerUnit = item.CostPerUnit;
            SalePrice = item.SalePrice;
            QuantityOnHand = item.QuantityOnHand;
            MinimumStockLevel = item.MinimumStockLevel;
            Supplier = item.Supplier;
            Notes = item.Notes;
            ImagePath = item.ImagePath;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsValid);
            CancelCommand = new RelayCommand(Cancel);
            BrowseImageCommand = new RelayCommand(async () => await BrowseImageAsync());
            AddBomComponentCommand = new RelayCommand(AddBomComponent, () => CanAddComponent);
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            var all = await _inventoryService.GetAllItemsAsync();

            AvailableComponents.Clear();
            foreach (var item in all.Where(i => i.Id != _originalItem.Id).OrderBy(i => i.Name))
                AvailableComponents.Add(item);

            if (_isEditMode)
            {
                var existing = await _bomService.GetComponentsForItemAsync(_originalItem.Id);
                foreach (var comp in existing)
                {
                    var compItem = all.FirstOrDefault(i => i.Id == comp.ComponentItemId);
                    if (compItem != null)
                    {
                        var entry = new BomComponentEntry(compItem, comp.QuantityPerUnit, RemoveBomComponent)
                        {
                            ExistingId = comp.Id
                        };
                        BomComponents.Add(entry);
                    }
                }
            }
        }

        private void AddBomComponent()
        {
            if (PendingComponent == null || PendingQty <= 0) return;
            if (BomComponents.Any(e => e.Item.Id == PendingComponent.Id)) return;

            BomComponents.Add(new BomComponentEntry(PendingComponent, PendingQty, RemoveBomComponent));
            PendingComponent = null;
            PendingQty = 1m;
        }

        private void RemoveBomComponent(BomComponentEntry entry)
        {
            BomComponents.Remove(entry);
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            try
            {
                _originalItem.Name = Name;
                _originalItem.Sku = Sku;
                _originalItem.Category = Category;
                _originalItem.CostPerUnit = CostPerUnit;
                _originalItem.SalePrice = SalePrice;
                _originalItem.QuantityOnHand = QuantityOnHand;
                _originalItem.MinimumStockLevel = MinimumStockLevel;
                _originalItem.Supplier = Supplier;
                _originalItem.Notes = Notes;
                _originalItem.ImagePath = ImagePath;

                await _inventoryService.CreateOrUpdateItemAsync(_originalItem);

                var components = BomComponents.Select(e => new BomComponent
                {
                    Id = e.ExistingId ?? Guid.NewGuid(),
                    ParentItemId = _originalItem.Id,
                    ComponentItemId = e.Item.Id,
                    QuantityPerUnit = e.QuantityPerUnit
                });
                await _bomService.SaveComponentsForItemAsync(_originalItem.Id, components);

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to save inventory item: {ItemName}", Name);
                await _dialogService.ShowAlertAsync("Save failed", $"Could not save the item:\n\n{ex.Message}");
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }

        private async Task BrowseImageAsync()
        {
            var stored = await _filePicker.PickAndStoreImageAsync("Select Product Image");
            if (stored != null)
                ImagePath = stored;
        }
    }

    public sealed class BomComponentEntry
    {
        public InventoryItem Item { get; }
        public decimal QuantityPerUnit { get; }
        public Guid? ExistingId { get; set; }
        public ICommand RemoveCommand { get; }

        public string QuantityDisplay => $"× {QuantityPerUnit:G}";

        public BomComponentEntry(InventoryItem item, decimal quantityPerUnit, Action<BomComponentEntry> onRemove)
        {
            Item = item;
            QuantityPerUnit = quantityPerUnit;
            RemoveCommand = new RelayCommand(() => onRemove(this));
        }
    }
}
