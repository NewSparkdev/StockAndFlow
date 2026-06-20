using System;
using System.ComponentModel.DataAnnotations;

namespace StockAndFlow.Models
{
    public class Expense
    {
        public Guid Id { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Category must be between 1 and 100 characters")]
        public string Category { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
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
