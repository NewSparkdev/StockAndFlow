using System;
using System.ComponentModel.DataAnnotations;

namespace StockAndFlow.Models
{
    public class Sale
    {
        private Guid _transactionId;

        public Guid Id { get; set; }

        public Guid TransactionId
        {
            get
            {
                // If TransactionId is empty (existing records), generate one
                if (_transactionId == Guid.Empty)
                {
                    _transactionId = Guid.NewGuid();
                }
                return _transactionId;
            }
            set => _transactionId = value;
        }

        [Required]
        public DateTime SaleDate { get; set; }

        [Required]
        public Guid InventoryItemId { get; set; }

        [Required(ErrorMessage = "Item name is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Item name must be between 1 and 200 characters")]
        public string ItemName { get; set; } = string.Empty;

        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Sale price must be 0 or greater")]
        public decimal SalePricePerUnit { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Cost per unit must be 0 or greater")]
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// Unit the item was sold in ("each", "oz", "lb", …), captured at sale time so an
        /// invoice reprinted years later still shows the unit it was actually sold in, even
        /// if the inventory item has since been changed.
        /// </summary>
        public string UnitOfMeasure { get; set; } = "each";

        /// <summary>True when this line was sold by weight/volume rather than counted.</summary>
        public bool IsMeasured =>
            !string.IsNullOrWhiteSpace(UnitOfMeasure) &&
            !string.Equals(UnitOfMeasure, "each", StringComparison.OrdinalIgnoreCase);

        /// <summary>Quantity for display: "3" when counted, "2.5 oz" when measured.</summary>
        public string QuantityDisplay => IsMeasured
            ? $"{Quantity:0.###} {UnitOfMeasure}"
            : $"{Quantity:0.###}";

        // Optional reference to Shopify order
        public string? ShopifyOrderId { get; set; }
        public string? ShopifyOrderNumber { get; set; }

        // Customer info (optional)
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public Guid? CustomerId { get; set; }

        public string? Notes { get; set; }

        // Tax information
        public string? TaxStateCode { get; set; }
        public decimal TaxRate { get; set; } // As percentage (e.g., 7.5 for 7.5%)
        public decimal TaxAmount { get; set; }

        // Calculated properties
        public decimal Subtotal => Quantity * SalePricePerUnit;
        public decimal Revenue => Subtotal + TaxAmount; // Total including tax
        public decimal COGS => Quantity * CostPerUnit;
        public decimal Profit => Revenue - COGS;
        public decimal ProfitMargin => Revenue > 0 ? (Profit / Revenue) * 100 : 0;

        public Sale()
        {
            Id = Guid.NewGuid();
            _transactionId = Guid.NewGuid();
            SaleDate = DateTime.Now;
        }
    }
}
