using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StockAndFlow.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private decimal _quantity = 1;
        private decimal _salePricePerUnit;

        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal CostPerUnit { get; set; }
        public decimal AvailableQuantity { get; set; }

        /// <summary>How the item is measured ("each", "oz", "lb", ...). Used for display only.</summary>
        public string UnitOfMeasure { get; set; } = "each";
        public bool IsMeasured => !string.IsNullOrEmpty(UnitOfMeasure) &&
            !string.Equals(UnitOfMeasure, "each", StringComparison.OrdinalIgnoreCase);
        public string AvailableDisplay => IsMeasured
            ? $"{AvailableQuantity:0.###} {UnitOfMeasure}"
            : AvailableQuantity.ToString("0.###");

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(ItemCOGS));
                    OnPropertyChanged(nameof(ItemProfit));
                }
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(ItemCOGS));
                    OnPropertyChanged(nameof(ItemProfit));
                }
            }
        }

        public decimal SalePricePerUnit
        {
            get => _salePricePerUnit;
            set
            {
                if (_salePricePerUnit != value)
                {
                    _salePricePerUnit = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(ItemCOGS));
                    OnPropertyChanged(nameof(ItemProfit));
                }
            }
        }

        // Calculated properties
        public decimal Subtotal => IsSelected ? Quantity * SalePricePerUnit : 0;
        public decimal ItemCOGS => IsSelected ? Quantity * CostPerUnit : 0;
        public decimal ItemProfit => Subtotal - ItemCOGS;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
