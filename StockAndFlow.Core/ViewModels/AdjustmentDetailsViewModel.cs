using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class AdjustmentDetailsViewModel : ViewModelBase
    {
        private readonly InventoryAdjustmentService _adjustmentService;
        private readonly InventoryService _inventoryService;
        private InventoryAdjustment _adjustment;
        private DateTime _adjustmentDate;
        private decimal? _currentQuantity;

        public event EventHandler<bool>? CloseRequested;

        public InventoryAdjustment Adjustment
        {
            get => _adjustment;
            set => SetProperty(ref _adjustment, value);
        }

        public DateTime AdjustmentDate
        {
            get => _adjustmentDate;
            set => SetProperty(ref _adjustmentDate, value);
        }

        public decimal? CurrentQuantity
        {
            get => _currentQuantity;
            set => SetProperty(ref _currentQuantity, value);
        }

        public decimal? QuantityBefore => CurrentQuantity.HasValue ? CurrentQuantity.Value - Adjustment.QuantityChange : null;
        public decimal? QuantityAfter => CurrentQuantity;

        // Financial impact properties
        public decimal CostPerUnit => Adjustment?.CostPerUnit ?? 0;
        public decimal SalePricePerUnit => Adjustment?.SalePricePerUnit ?? 0;
        public decimal QuantityLost => Adjustment != null && Adjustment.QuantityChange < 0 ? Math.Abs(Adjustment.QuantityChange) : 0;
        public decimal CostLost => Adjustment?.TotalCost ?? 0;
        public decimal PotentialRevenueLost => Adjustment?.PotentialRevenue ?? 0;
        public decimal PotentialProfitLost => Adjustment?.PotentialProfit ?? 0;
        public bool IsLoss => Adjustment?.IsLoss ?? false;

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public AdjustmentDetailsViewModel(
            InventoryAdjustmentService adjustmentService,
            InventoryService inventoryService,
            InventoryAdjustment adjustment)
        {
            _adjustmentService = adjustmentService;
            _inventoryService = inventoryService;
            _adjustment = adjustment;
            _adjustmentDate = adjustment.AdjustmentDate;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CloseCommand = new RelayCommand(Close);

            _ = LoadCurrentQuantityAsync();
        }

        private async Task LoadCurrentQuantityAsync()
        {
            try
            {
                var item = await _inventoryService.GetItemByIdAsync(Adjustment.InventoryItemId);
                if (item != null)
                {
                    CurrentQuantity = item.QuantityOnHand;
                    OnPropertyChanged(nameof(QuantityBefore));
                    OnPropertyChanged(nameof(QuantityAfter));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading current quantity: {ex.Message}");
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Update the adjustment date if changed
                if (_adjustment.AdjustmentDate != AdjustmentDate)
                {
                    _adjustment.AdjustmentDate = AdjustmentDate;
                    await _adjustmentService.UpdateAdjustmentAsync(_adjustment);
                }

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating adjustment: {ex.Message}");
                CloseRequested?.Invoke(this, false);
            }
        }

        private void Close()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
