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

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Sale price must be 0 or greater")]
        public decimal SalePricePerUnit { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Cost per unit must be 0 or greater")]
        public decimal CostPerUnit { get; set; }

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
