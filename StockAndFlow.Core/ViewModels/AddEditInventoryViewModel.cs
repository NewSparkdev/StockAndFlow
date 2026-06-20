using System;
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
        private readonly IFilePickerService _filePicker;
        private readonly InventoryItem _originalItem;
        private readonly bool _isEditMode;

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

        public event EventHandler<bool>? CloseRequested;

        public string DialogTitle => _isEditMode ? "Edit Inventory Item" : "Add New Inventory Item";

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
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
                {
                    OnPropertyChanged(nameof(IsValid));
                }
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
                {
                    OnPropertyChanged(nameof(HasImage));
                }
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

        // Constructor for adding new item
        public AddEditInventoryViewModel(InventoryService inventoryService, IFilePickerService filePicker)
        {
            _inventoryService = inventoryService;
            _filePicker = filePicker;
            _originalItem = new InventoryItem();
            _isEditMode = false;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsValid);
            CancelCommand = new RelayCommand(Cancel);
            BrowseImageCommand = new RelayCommand(async () => await BrowseImageAsync());
        }

        // Constructor for editing existing item
        public AddEditInventoryViewModel(InventoryService inventoryService, InventoryItem item, IFilePickerService filePicker)
        {
            _inventoryService = inventoryService;
            _filePicker = filePicker;
            _originalItem = item;
            _isEditMode = true;

            // Load existing values
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
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            try
            {
                // Update the item with current values
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

                // Save to database
                await _inventoryService.CreateOrUpdateItemAsync(_originalItem);

                // Close dialog with success
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to save inventory item: {ItemName}", Name);
                CloseRequested?.Invoke(this, false);
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
            {
                ImagePath = stored;
            }
        }
    }
}
