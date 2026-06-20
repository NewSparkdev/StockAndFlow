using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class RecordAdjustmentViewModel : ViewModelBase
    {
        private readonly InventoryAdjustmentService _adjustmentService;
        private readonly InventoryService _inventoryService;

        private ObservableCollection<InventoryItem> _inventoryItems = new();
        private InventoryItem? _selectedItem;
        private AdjustmentReason _selectedReason;
        private int _quantityChange;
        private string? _notes;
        private DateTime _adjustmentDate;

        public event EventHandler<bool>? CloseRequested;

        public ObservableCollection<InventoryItem> InventoryItems
        {
            get => _inventoryItems;
            set => SetProperty(ref _inventoryItems, value);
        }

        public InventoryItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(CurrentQuantity));
                    OnPropertyChanged(nameof(NewQuantity));
                    OnPropertyChanged(nameof(CostPerUnit));
                    OnPropertyChanged(nameof(SalePricePerUnit));
                    OnPropertyChanged(nameof(QuantityLost));
                    OnPropertyChanged(nameof(CostLost));
                    OnPropertyChanged(nameof(PotentialRevenueLost));
                    OnPropertyChanged(nameof(PotentialProfitLost));
                    OnPropertyChanged(nameof(IsLoss));
                }
            }
        }

        public List<AdjustmentReasonItem> AdjustmentReasons { get; }

        public AdjustmentReason SelectedReason
        {
            get => _selectedReason;
            set => SetProperty(ref _selectedReason, value);
        }

        public int QuantityChange
        {
            get => _quantityChange;
            set
            {
                if (SetProperty(ref _quantityChange, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(NewQuantity));
                    OnPropertyChanged(nameof(QuantityLost));
                    OnPropertyChanged(nameof(CostLost));
                    OnPropertyChanged(nameof(PotentialRevenueLost));
                    OnPropertyChanged(nameof(PotentialProfitLost));
                    OnPropertyChanged(nameof(IsLoss));
                }
            }
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public DateTime AdjustmentDate
        {
            get => _adjustmentDate;
            set => SetProperty(ref _adjustmentDate, value);
        }

        public int CurrentQuantity => SelectedItem?.QuantityOnHand ?? 0;
        public int NewQuantity => CurrentQuantity + QuantityChange;

        // Financial impact properties
        public decimal CostPerUnit => SelectedItem?.CostPerUnit ?? 0;
        public decimal SalePricePerUnit => SelectedItem?.SalePrice ?? 0;
        public int QuantityLost => QuantityChange < 0 ? Math.Abs(QuantityChange) : 0;
        public decimal CostLost => QuantityLost * CostPerUnit;
        public decimal PotentialRevenueLost => QuantityLost * SalePricePerUnit;
        public decimal PotentialProfitLost => PotentialRevenueLost - CostLost;
        public bool IsLoss => QuantityChange < 0;

        public bool IsValid => SelectedItem != null && QuantityChange != 0;

        public ICommand RecordCommand { get; }
        public ICommand CancelCommand { get; }

        public RecordAdjustmentViewModel(
            InventoryAdjustmentService adjustmentService,
            InventoryService inventoryService)
        {
            _adjustmentService = adjustmentService;
            _inventoryService = inventoryService;

            _adjustmentDate = DateTime.Now;

            AdjustmentReasons = new List<AdjustmentReasonItem>
            {
                new AdjustmentReasonItem(AdjustmentReason.Damaged, "Damaged"),
                new AdjustmentReasonItem(AdjustmentReason.CustomerReturn, "Customer Return"),
                new AdjustmentReasonItem(AdjustmentReason.Lost, "Lost/Stolen"),
                new AdjustmentReasonItem(AdjustmentReason.Expired, "Expired"),
                new AdjustmentReasonItem(AdjustmentReason.Found, "Found"),
                new AdjustmentReasonItem(AdjustmentReason.Correction, "Inventory Correction"),
                new AdjustmentReasonItem(AdjustmentReason.Other, "Other")
            };

            RecordCommand = new RelayCommand(async () => await RecordAdjustmentAsync(), () => IsValid);
            CancelCommand = new RelayCommand(Cancel);

            _ = LoadInventoryItemsAsync();
        }

        private async Task LoadInventoryItemsAsync()
        {
            var items = await _inventoryService.GetAllItemsAsync();
            InventoryItems = new ObservableCollection<InventoryItem>(items.OrderBy(i => i.Name));
        }

        private async Task RecordAdjustmentAsync()
        {
            if (SelectedItem == null)
                return;

            try
            {
                var adjustment = new InventoryAdjustment
                {
                    InventoryItemId = SelectedItem.Id,
                    InventoryItemName = SelectedItem.Name,
                    Reason = SelectedReason,
                    QuantityChange = QuantityChange,
                    CostPerUnit = SelectedItem.CostPerUnit,
                    SalePricePerUnit = SelectedItem.SalePrice,
                    Notes = Notes,
                    AdjustmentDate = AdjustmentDate
                };

                await _adjustmentService.RecordAdjustmentAsync(adjustment);
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recording adjustment: {ex.Message}");
                CloseRequested?.Invoke(this, false);
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }
    }

    public class AdjustmentReasonItem
    {
        public AdjustmentReason Reason { get; set; }
        public string Display { get; set; }

        public AdjustmentReasonItem(AdjustmentReason reason, string display)
        {
            Reason = reason;
            Display = display;
        }
    }
}
