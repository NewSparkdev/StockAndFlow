using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StockAndFlow.Models
{
    public class InventoryItem
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Item name is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "SKU cannot exceed 100 characters")]
        public string? Sku { get; set; }

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
        public string? Category { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Cost per unit must be 0 or greater")]
        public decimal CostPerUnit { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Sale price must be 0 or greater")]
        public decimal SalePrice { get; set; }

        public int QuantityOnHand { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Minimum stock level must be 0 or greater")]
        public int MinimumStockLevel { get; set; } = 0;
        public string? Supplier { get; set; }
        public string? Notes { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        // Soft delete support
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedDate { get; set; }

        // Optional Shopify integration
        public string? ShopifyProductId { get; set; }
        public string? ShopifyVariantId { get; set; }
        public DateTime? LastSyncedAt { get; set; }

        // Calculated properties (not persisted, calculated on load)
        public decimal TotalValue => CostPerUnit * QuantityOnHand;
        public decimal ProfitPerUnit => SalePrice - CostPerUnit;
        public decimal ProfitMarginPerUnit => SalePrice - CostPerUnit; // Alias for backward compatibility
        public decimal ProfitMarginPercentage => SalePrice > 0
            ? (ProfitPerUnit / SalePrice) * 100
            : 0;

        // UI Helper properties
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);
        public bool IsSyncedWithShopify => !string.IsNullOrEmpty(ShopifyProductId);
        public string Description => Notes ?? string.Empty;

        // Stock level helper properties
        public bool IsOutOfStock => QuantityOnHand <= 0;
        public bool IsLowStock => QuantityOnHand > 0 && QuantityOnHand <= MinimumStockLevel;
        public bool IsInStock => QuantityOnHand > MinimumStockLevel;
        public string StockStatus
        {
            get
            {
                if (IsOutOfStock) return "Out of Stock";
                if (IsLowStock) return "Low Stock";
                return "In Stock";
            }
        }
        public string StockStatusColor
        {
            get
            {
                if (IsOutOfStock) return "#E74C3C"; // Red
                if (IsLowStock) return "#F39C12"; // Orange
                return "#27AE60"; // Green
            }
        }

        public InventoryItem()
        {
            Id = Guid.NewGuid();
            CreatedDate = DateTime.Now;
            LastModifiedDate = DateTime.Now;
        }
    }
}
