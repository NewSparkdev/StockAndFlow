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
    public class RecordSaleViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly InventoryService _inventoryService;
        private readonly BusinessSettingsService _businessSettingsService;
        private readonly InvoiceService _invoiceService;

        private string? _customerName;
        private string? _customerEmail;
        private string? _notes;
        private ObservableCollection<CartItem> _allItems = new();
        private SaleTransaction? _lastTransaction;
        private StateTaxInfo? _selectedState;
        private ObservableCollection<StateTaxInfo> _states = new();

        public event EventHandler<bool>? CloseRequested;
        public event EventHandler<SaleTransaction>? SaleCompleted;
        public event EventHandler<string>? SaleFailed;

        public ObservableCollection<CartItem> AllItems
        {
            get => _allItems;
            set => SetProperty(ref _allItems, value);
        }

        public string? CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        public string? CustomerEmail
        {
            get => _customerEmail;
            set => SetProperty(ref _customerEmail, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public ObservableCollection<StateTaxInfo> States
        {
            get => _states;
            set => SetProperty(ref _states, value);
        }

        public StateTaxInfo? SelectedState
        {
            get => _selectedState;
            set
            {
                if (SetProperty(ref _selectedState, value))
                {
                    UpdateCartProperties();
                }
            }
        }

        // Cart calculated properties
        public decimal CartSubtotal => AllItems.Where(i => i.IsSelected).Sum(item => item.Subtotal);
        public decimal TaxRate => SelectedState?.TaxRate ?? 0;
        public decimal TaxAmount => CartSubtotal * (TaxRate / 100);
        public decimal CartTotal => CartSubtotal + TaxAmount;
        public decimal CartTotalProfit => AllItems.Where(i => i.IsSelected).Sum(item => item.ItemProfit);
        public int SelectedItemCount => AllItems.Count(i => i.IsSelected);
        public bool HasSelectedItems => SelectedItemCount > 0;

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public RecordSaleViewModel(SalesService salesService, InventoryService inventoryService,
            BusinessSettingsService businessSettingsService, InvoiceService invoiceService)
        {
            _salesService = salesService;
            _inventoryService = inventoryService;
            _businessSettingsService = businessSettingsService;
            _invoiceService = invoiceService;

            // Load states
            States = new ObservableCollection<StateTaxInfo>(StateTaxInfo.GetAllStates());
            SelectedState = States.FirstOrDefault(s => s.StateCode == "NONE"); // Default to No Tax

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => HasSelectedItems);
            CancelCommand = new RelayCommand(Cancel);

            _ = LoadItemsAsync();
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                var items = await _inventoryService.GetAllItemsAsync();

                var cartItems = new System.Collections.Generic.List<CartItem>();
                foreach (var item in items.OrderBy(i => i.Name))
                {
                    var cartItem = new CartItem
                    {
                        InventoryItemId = item.Id,
                        ItemName = item.Name,
                        SalePricePerUnit = item.SalePrice,
                        CostPerUnit = item.CostPerUnit,
                        AvailableQuantity = item.QuantityOnHand,
                        Quantity = 1,
                        IsSelected = false
                    };

                    // Subscribe to property changes to update cart totals
                    cartItem.PropertyChanged += (s, e) => UpdateCartProperties();

                    cartItems.Add(cartItem);
                }

                // Assign the bound collection on the UI thread.
                UiDispatcher.Run(() => AllItems = new ObservableCollection<CartItem>(cartItems));
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load inventory items for sale recording");
            }
        }

        private void UpdateCartProperties()
        {
            OnPropertyChanged(nameof(CartSubtotal));
            OnPropertyChanged(nameof(TaxRate));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(CartTotal));
            OnPropertyChanged(nameof(CartTotalProfit));
            OnPropertyChanged(nameof(SelectedItemCount));
            OnPropertyChanged(nameof(HasSelectedItems));
        }

        private async Task SaveAsync()
        {
            if (!HasSelectedItems)
                return;

            // Pre-validate quantities against available stock so we never partially commit a
            // multi-item sale and then fail mid-loop.
            var problem = AllItems
                .Where(i => i.IsSelected)
                .FirstOrDefault(i => i.Quantity <= 0 || i.Quantity > i.AvailableQuantity);
            if (problem != null)
            {
                SaleFailed?.Invoke(this,
                    $"Not enough stock for \"{problem.ItemName}\".\nAvailable: {problem.AvailableQuantity}, requested: {problem.Quantity}.");
                return;
            }

            try
            {
                // Generate a transaction ID and timestamp for this multi-item sale
                var transactionId = Guid.NewGuid();
                var saleDate = DateTime.Now;
                var allSalesSucceeded = true;

                // Calculate tax for each item proportionally
                var selectedItems = AllItems.Where(i => i.IsSelected).ToList();
                var totalSubtotal = selectedItems.Sum(i => i.Subtotal);

                // Record each selected item as a separate sale with the same transaction ID
                foreach (var item in selectedItems)
                {
                    // Calculate proportional tax for this item
                    var itemSubtotal = item.Subtotal;
                    var itemTaxAmount = totalSubtotal > 0 ? (itemSubtotal / totalSubtotal) * TaxAmount : 0;

                    var sale = await _salesService.RecordSaleAsync(
                        item.InventoryItemId,
                        item.Quantity,
                        item.SalePricePerUnit,
                        CustomerName,
                        CustomerEmail,
                        Notes,
                        transactionId,
                        saleDate,
                        SelectedState?.StateCode,
                        TaxRate,
                        itemTaxAmount);

                    if (sale == null)
                    {
                        allSalesSucceeded = false;
                        break;
                    }
                }

                if (allSalesSucceeded)
                {
                    // Create the transaction object
                    var transaction = new SaleTransaction
                    {
                        TransactionId = transactionId,
                        SaleDate = saleDate,
                        CustomerName = CustomerName,
                        CustomerEmail = CustomerEmail,
                        Notes = Notes,
                        Items = selectedItems.Select(cartItem =>
                        {
                            // Calculate proportional tax for this item
                            var itemSubtotal = cartItem.Subtotal;
                            var itemTaxAmount = totalSubtotal > 0 ? (itemSubtotal / totalSubtotal) * TaxAmount : 0;

                            return new Sale
                            {
                                Id = Guid.NewGuid(),
                                TransactionId = transactionId,
                                SaleDate = saleDate,
                                ItemName = cartItem.ItemName,
                                Quantity = cartItem.Quantity,
                                SalePricePerUnit = cartItem.SalePricePerUnit,
                                CostPerUnit = cartItem.CostPerUnit,
                                CustomerName = CustomerName,
                                CustomerEmail = CustomerEmail,
                                Notes = Notes,
                                TaxStateCode = SelectedState?.StateCode,
                                TaxRate = TaxRate,
                                TaxAmount = itemTaxAmount
                            };
                        }).ToList()
                    };

                    _lastTransaction = transaction;

                    // Fire SaleCompleted event before closing
                    SaleCompleted?.Invoke(this, transaction);

                    // All sales recorded successfully - close will be handled by dialog
                }
                else
                {
                    // Sale failed (probably due to insufficient inventory)
                    var firstItem = AllItems.Where(i => i.IsSelected).FirstOrDefault();
                    LogWarning("Sale recording failed - insufficient inventory or item not found. Item: {ItemName}, Quantity: {Quantity}",
                        firstItem?.ItemName ?? "Unknown", firstItem?.Quantity);
                    SaleFailed?.Invoke(this, "The sale could not be completed. Inventory may have changed — please review and try again.");
                }
            }
            catch (Exception ex)
            {
                var firstItem = AllItems.Where(i => i.IsSelected).FirstOrDefault();
                LogError(ex, "Failed to record sale for item: {ItemName}", firstItem?.ItemName ?? "Unknown");
                SaleFailed?.Invoke(this, $"The sale could not be completed:\n\n{ex.Message}");
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }

        public async Task<string?> GenerateInvoiceAsync(SaleTransaction transaction, string? outputPath = null)
        {
            try
            {
                // Check if business settings are configured
                var isConfigured = await _businessSettingsService.IsConfiguredAsync();
                if (!isConfigured)
                {
                    return null; // Caller should prompt for business settings
                }

                // Generate the invoice (outputPath lets mobile target a sandbox-writable location;
                // desktop passes null and the service uses the user's Downloads folder).
                var invoicePath = await _invoiceService.GenerateInvoiceAsync(transaction, outputPath);
                return invoicePath;
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to generate invoice for transaction");
                return null;
            }
        }

        public async Task<bool> IsBusinessConfiguredAsync()
        {
            return await _businessSettingsService.IsConfiguredAsync();
        }
    }
}
