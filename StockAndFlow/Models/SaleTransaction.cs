using System;
using System.Collections.Generic;
using System.Linq;

namespace StockAndFlow.Models
{
    public class SaleTransaction
    {
        public Guid TransactionId { get; set; }
        public DateTime SaleDate { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Notes { get; set; }
        public List<Sale> Items { get; set; } = new();

        // Calculated properties
        public int ItemCount => Items?.Count ?? 0;
        public int TotalQuantity => Items?.Sum(i => i.Quantity) ?? 0;
        public decimal Revenue => Items?.Sum(i => i.Revenue) ?? 0;
        public decimal COGS => Items?.Sum(i => i.COGS) ?? 0;
        public decimal Profit => Items?.Sum(i => i.Profit) ?? 0;
        public decimal ProfitMargin => Revenue > 0 ? (Profit / Revenue) * 100 : 0;

        // Display property for item names
        public string ItemNames
        {
            get
            {
                if (Items == null || Items.Count == 0) return "";
                if (Items.Count == 1) return Items[0].ItemName ?? "";
                if (Items.Count == 2) return $"{Items[0].ItemName}, {Items[1].ItemName}";
                return $"{Items[0].ItemName}, {Items[1].ItemName}, +{Items.Count - 2} more";
            }
        }

        public string ItemSummary
        {
            get
            {
                if (Items == null || Items.Count == 0) return "No items";
                if (Items.Count == 1) return $"{Items[0].Quantity}x {Items[0].ItemName}";
                return $"{Items.Count} items ({TotalQuantity} total units)";
            }
        }
    }
}
