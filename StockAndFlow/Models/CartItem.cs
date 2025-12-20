using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StockAndFlow.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _quantity = 1;
        private decimal _salePricePerUnit;

        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal CostPerUnit { get; set; }
        public int AvailableQuantity { get; set; }

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

        public int Quantity
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
