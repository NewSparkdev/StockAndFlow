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
    /// <summary>
    /// Wraps a single Sale line-item with editable quantity and price for the edit form.
    /// </summary>
    public class EditableSaleItem : ViewModelBase
    {
        private decimal _quantity;
        private decimal _salePricePerUnit;

        public Sale OriginalSale { get; }
        public string ItemName => OriginalSale.ItemName;

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public decimal SalePricePerUnit
        {
            get => _salePricePerUnit;
            set => SetProperty(ref _salePricePerUnit, value);
        }

        public EditableSaleItem(Sale sale)
        {
            OriginalSale = sale;
            _quantity = sale.Quantity;
            _salePricePerUnit = sale.SalePricePerUnit;
        }
    }

    public class EditSaleViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly InventoryService _inventoryService;
        private readonly BomService _bomService;
        private readonly SaleTransaction _transaction;
        private readonly IDialogService _dialogService;

        private DateTime _saleDate;
        private string? _customerName;
        private string? _customerEmail;
        private string? _notes;

        public event EventHandler<bool>? CloseRequested;

        public string DialogTitle => "Edit Sale";

        public ObservableCollection<EditableSaleItem> Items { get; }

        public DateTime SaleDate
        {
            get => _saleDate;
            set => SetProperty(ref _saleDate, value);
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

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditSaleViewModel(SalesService salesService, InventoryService inventoryService,
            BomService bomService, SaleTransaction transaction, IDialogService dialogService)
        {
            _salesService = salesService;
            _inventoryService = inventoryService;
            _bomService = bomService;
            _transaction = transaction;
            _dialogService = dialogService;

            _saleDate = transaction.SaleDate;
            _customerName = transaction.CustomerName;
            _customerEmail = transaction.CustomerEmail;
            _notes = transaction.Notes;

            Items = new ObservableCollection<EditableSaleItem>(
                transaction.Items.Select(s => new EditableSaleItem(s)));

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(Cancel);
        }

        private async Task SaveAsync()
        {
            try
            {
                foreach (var editableItem in Items)
                {
                    var sale = editableItem.OriginalSale;
                    decimal quantityDelta = editableItem.Quantity - sale.Quantity;

                    sale.SaleDate = SaleDate;
                    sale.CustomerName = CustomerName;
                    sale.CustomerEmail = CustomerEmail;
                    sale.Notes = Notes;
                    sale.Quantity = editableItem.Quantity;
                    sale.SalePricePerUnit = editableItem.SalePricePerUnit;

                    // Positive delta = sold more → deduct more from inventory.
                    // Negative delta = sold less → restore the difference.
                    // Forced: the sale record is changing regardless, so stock must follow it
                    // even below zero — same rule as BOM deductions when the sale was recorded.
                    if (quantityDelta != 0)
                    {
                        await _inventoryService.AdjustQuantityAsync(sale.InventoryItemId, -quantityDelta, forceAllowNegative: true);

                        var components = await _bomService.GetComponentsForItemAsync(sale.InventoryItemId);
                        foreach (var comp in components)
                        {
                            var change = comp.QuantityPerUnit * quantityDelta;
                            if (change != 0)
                                await _inventoryService.AdjustQuantityAsync(comp.ComponentItemId, -change, forceAllowNegative: true);
                        }
                    }

                    await _salesService.UpdateSaleAsync(sale);
                }

                // Keep the in-memory transaction object in sync so the details page refreshes.
                _transaction.SaleDate = SaleDate;
                _transaction.CustomerName = CustomerName;
                _transaction.CustomerEmail = CustomerEmail;
                _transaction.Notes = Notes;

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to save sale edit");
                await _dialogService.ShowAlertAsync("Save failed", $"Could not save the sale:\n\n{ex.Message}");
            }
        }

        private void Cancel() => CloseRequested?.Invoke(this, false);
    }
}
