using System;

namespace StockAndFlow.Models
{
    public class Expense
    {
        public Guid Id { get; set; }
        public DateTime ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Optional link to inventory item
        public Guid? InventoryItemId { get; set; }
        public string? LinkedItemName { get; set; }

        public string ReceiptImagePath { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        // UI Helper properties
        public bool HasReceipt => !string.IsNullOrEmpty(ReceiptImagePath);
        public bool HasLinkedItem => InventoryItemId.HasValue;
        public Guid LinkedInventoryItemId => InventoryItemId ?? Guid.Empty;

        public Expense()
        {
            Id = Guid.NewGuid();
            ExpenseDate = DateTime.Now;
            CreatedDate = DateTime.Now;
        }
    }
}
