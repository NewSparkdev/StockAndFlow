using System;

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

        public DateTime SaleDate { get; set; }
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal SalePricePerUnit { get; set; }
        public decimal CostPerUnit { get; set; }

        // Optional reference to Shopify order
        public string? ShopifyOrderId { get; set; }
        public string? ShopifyOrderNumber { get; set; }

        // Customer info (optional)
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

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
