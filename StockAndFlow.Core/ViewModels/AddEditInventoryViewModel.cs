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
        private decimal _extraCost;
        private decimal _salePrice;
        private decimal _quantityOnHand;
        private decimal _minimumStockLevel;
        private UnitOption _selectedUnit = UnitOption.Each;
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

        public bool HasBom => BomComponents.Count > 0;

        /// <summary>What one unit of this item costs in raw materials: Σ(component cost × amount used).</summary>
        public decimal MaterialsCost => BomComponents.Sum(e => e.Item.CostPerUnit * e.QuantityPerUnit);

        /// <summary>
        /// The user's own costs on top of materials (labor, packaging). Never touched by the
        /// app, so refreshing material prices can't overwrite it.
        /// </summary>
        public decimal ExtraCost
        {
            get => _extraCost;
            set
            {
                if (SetProperty(ref _extraCost, value))
                {
                    OnPropertyChanged(nameof(CostBreakdownHint));
                    if (HasBom)
                        CostPerUnit = MaterialsCost + ExtraCost;
                }
            }
        }

        public string CostBreakdownHint =>
            $"Calculated for you: materials {MaterialsCost:C2} + your extras {ExtraCost:C2}";

        public string ExtraCostHelpText =>
            "Anything else that goes into making ONE of this item besides the materials listed here — your time, packaging, labels, shipping supplies.\n\n" +
            "Example: materials come to $0.90 and you value your labor at $0.35 per candle — enter 0.35 and Your cost becomes $1.25.\n\n" +
            "This number is yours: updating material prices never changes it. Leave it at 0 if materials are the whole cost.";

        /// <summary>
        /// Keeps "Your cost" in sync while a Bill of Materials exists:
        /// total = materials (calculated) + extras (user-owned).
        /// </summary>
        private void OnBomChanged()
        {
            OnPropertyChanged(nameof(HasBom));
            OnPropertyChanged(nameof(MaterialsCost));
            OnPropertyChanged(nameof(CostBreakdownHint));
            if (BomComponents.Count > 0)
                CostPerUnit = MaterialsCost + ExtraCost;
        }

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
                    OnPropertyChanged(nameof(ProfitSummary));
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
                    OnPropertyChanged(nameof(ProfitSummary));
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public decimal QuantityOnHand
        {
            get => _quantityOnHand;
            set
            {
                if (SetProperty(ref _quantityOnHand, value))
                    OnPropertyChanged(nameof(IsValid));
            }
        }

        public decimal MinimumStockLevel
        {
            get => _minimumStockLevel;
            set => SetProperty(ref _minimumStockLevel, value);
        }

        public System.Collections.Generic.List<UnitOption> UnitOptions { get; } = UnitOption.All;

        public UnitOption SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (value != null && SetProperty(ref _selectedUnit, value))
                {
                    OnPropertyChanged(nameof(IsMeasured));
                    OnPropertyChanged(nameof(QuantityLabel));
                    OnPropertyChanged(nameof(MinStockLabel));
                    OnPropertyChanged(nameof(CostLabel));
                    OnPropertyChanged(nameof(PriceLabel));
                    OnPropertyChanged(nameof(QuantityHelpText));
                    OnPropertyChanged(nameof(MinStockHelpText));
                    OnPropertyChanged(nameof(CostHelpText));
                    OnPropertyChanged(nameof(PriceHelpText));
                    OnPropertyChanged(nameof(ProfitSummary));
                }
            }
        }

        public bool IsMeasured => SelectedUnit.Value != "each";

        private string Unit => SelectedUnit.Value;

        // Unit-aware field labels
        public string QuantityLabel => IsMeasured ? $"How much in stock ({Unit})" : "How many in stock";
        public string MinStockLabel => "Low stock alert";
        public string CostLabel => IsMeasured ? $"Your cost (per {Unit})" : "Your cost (each)";
        public string PriceLabel => IsMeasured ? $"Selling price (per {Unit})" : "Selling price (each)";

        // Unit-aware help text for the tappable "?" icons
        public string BarcodeHelpText =>
            "There are two kinds of barcode, and most small sellers only need the first.\n\n" +
            "YOUR OWN (what this button makes)\n" +
            "Free, unlimited, and instantly usable. It scans with this app and with ordinary " +
            "barcode scanners, so it's all you need for craft fairs, markets, your own shop or " +
            "your own website. We'll create a short code for the item if it doesn't have one.\n\n" +
            "OFFICIAL RETAIL BARCODES (UPC / EAN)\n" +
            "The numbers on supermarket products. Required if you want to sell through a " +
            "retailer, a distributor or Amazon, because they have to be unique worldwide. They " +
            "are issued by GS1 for a fee — no app can make one for you, and inventing one would " +
            "clash with a real product.\n\n" +
            "ALREADY HAVE OFFICIAL BARCODES?\n" +
            "Use them here. Tap Scan and photograph the barcode, or just type the number into " +
            "the SKU / Barcode box. The app reads every common format and will store whatever " +
            "you give it.";

        public string MeasureHelpText =>
            "Most items are counted — 12 candles, 5 mugs — so leave this on \"By count\".\n\n" +
            "Pick a weight or volume unit for supplies you track in bulk, like candle wax. " +
            "Example: choose ounces (oz), and instead of \"90 waxes\" you'll have \"90 oz of wax\" — " +
            "amounts, prices, and low-stock alerts all become per ounce, and you can use partial amounts like 2.5 oz.";

        public string CostHelpText => IsMeasured
            ? $"What YOU pay for one {Unit} of this item.\n\nExample: a 90 oz bag of wax costs you $27 — that's $0.30 per oz, so you'd enter 0.30.\n\nThe app compares this with your selling price to show your profit."
            : "What YOU pay to buy or make one of this item.\n\nExample: you buy mugs from your supplier for $4 each, so you'd enter 4.\n\nIf you add a Bill of Materials below, this is calculated for you: materials cost + your extra costs (labor, packaging).\n\nThe app compares this with your selling price to show how much profit you make on every sale.";

        public string PriceHelpText => IsMeasured
            ? $"What your CUSTOMER pays for one {Unit} of this item, if you sell it directly.\n\nIf you only use this item as an ingredient in other products (like wax in candles), you can leave this at 0."
            : "What your CUSTOMER pays for one of this item.\n\nExample: you sell each mug for $10, so you'd enter 10.\n\nSelling price minus your cost = your profit on each one sold.";

        public string QuantityHelpText => IsMeasured
            ? $"How much of this item you have right now, in {Unit}. Weigh or measure what you have and enter that amount — partial amounts like 90.5 are fine.\n\nThe app lowers this automatically when sales use it up."
            : "The number of this item you have right now. Count what's on your shelf and enter that number.\n\nYou only set this when adding the item or fixing a count — the app lowers it automatically every time you record a sale.";

        public string MinStockHelpText => IsMeasured
            ? $"When your stock drops to this many {Unit}, the app flags the item so you know it's time to restock.\n\nExample: enter 10 and you'll see a 'Low stock' warning once only 10 {Unit} are left.\n\nEnter 0 if you don't want a restock reminder."
            : "When your stock drops to this number, the app flags the item so you know it's time to restock.\n\nExample: enter 5 and this item shows a 'Low stock' warning once only 5 are left.\n\nEnter 0 if you don't want a restock reminder for this item.";

        public string ProfitSummary => IsMeasured
            ? $"Profit per {Unit} sold: {ProfitMarginPerUnit:C2}"
            : $"Profit on each one sold: {ProfitMarginPerUnit:C2}";

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

            BomComponents.CollectionChanged += (_, _) => OnBomChanged();
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
            ExtraCost = item.ExtraCostPerUnit;
            SalePrice = item.SalePrice;
            QuantityOnHand = item.QuantityOnHand;
            MinimumStockLevel = item.MinimumStockLevel;
            SelectedUnit = UnitOption.FromValue(item.UnitOfMeasure);
            Supplier = item.Supplier;
            Notes = item.Notes;
            ImagePath = item.ImagePath;

            BomComponents.CollectionChanged += (_, _) => OnBomChanged();
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
                _originalItem.ExtraCostPerUnit = ExtraCost;
                _originalItem.SalePrice = SalePrice;
                _originalItem.QuantityOnHand = QuantityOnHand;
                _originalItem.MinimumStockLevel = MinimumStockLevel;
                _originalItem.UnitOfMeasure = SelectedUnit.Value;
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

    /// <summary>
    /// A choice in the "How do you measure this item?" picker: the stored unit value
    /// plus the friendly text shown to the user.
    /// </summary>
    public sealed class UnitOption
    {
        public string Value { get; }
        public string Display { get; }

        private UnitOption(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public override string ToString() => Display;

        public static readonly UnitOption Each = new("each", "By count (each)");

        public static readonly System.Collections.Generic.List<UnitOption> All = new()
        {
            Each,
            new("oz", "By weight — ounces (oz)"),
            new("lb", "By weight — pounds (lb)"),
            new("g", "By weight — grams (g)"),
            new("kg", "By weight — kilograms (kg)"),
            new("fl oz", "By volume — fluid ounces (fl oz)"),
            new("ml", "By volume — milliliters (ml)"),
            new("L", "By volume — liters (L)"),
        };

        public static UnitOption FromValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Each;
            return All.Find(u => string.Equals(u.Value, value, StringComparison.OrdinalIgnoreCase)) ?? Each;
        }
    }

    public sealed class BomComponentEntry
    {
        public InventoryItem Item { get; }
        public decimal QuantityPerUnit { get; }
        public Guid? ExistingId { get; set; }
        public ICommand RemoveCommand { get; }

        public string QuantityDisplay => Item.IsMeasured
            ? $"× {QuantityPerUnit:0.###} {Item.UnitOfMeasure}"
            : $"× {QuantityPerUnit:0.###}";

        /// <summary>What this component contributes to the cost of one finished item.</summary>
        public decimal LineCost => Item.CostPerUnit * QuantityPerUnit;
        public string LineCostDisplay => $"{LineCost:C2}";

        public BomComponentEntry(InventoryItem item, decimal quantityPerUnit, Action<BomComponentEntry> onRemove)
        {
            Item = item;
            QuantityPerUnit = quantityPerUnit;
            RemoveCommand = new RelayCommand(() => onRemove(this));
        }
    }
}
