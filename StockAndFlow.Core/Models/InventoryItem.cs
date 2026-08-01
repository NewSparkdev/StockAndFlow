using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StockAndFlow.Models
{
    public class InventoryItem : ISoftDeletable
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

        public decimal QuantityOnHand { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Minimum stock level must be 0 or greater")]
        public decimal MinimumStockLevel { get; set; } = 0;

        /// <summary>
        /// How the item is measured: "each" (counted, the default) or a weight/volume
        /// unit such as "oz", "lb", "g", "kg", "fl oz", "ml", "L".
        /// </summary>
        public string UnitOfMeasure { get; set; } = "each";
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
        public bool IsMeasured => !string.IsNullOrEmpty(UnitOfMeasure) &&
            !string.Equals(UnitOfMeasure, "each", StringComparison.OrdinalIgnoreCase);
        public string QuantityDisplay => IsMeasured
            ? $"{QuantityOnHand:0.###} {UnitOfMeasure}"
            : QuantityOnHand.ToString("0.###");
        public string MinimumStockDisplay => IsMeasured
            ? $"{MinimumStockLevel:0.###} {UnitOfMeasure}"
            : MinimumStockLevel.ToString("0.###");
        public string CostPerUnitDisplay => IsMeasured
            ? $"{CostPerUnit:C2} / {UnitOfMeasure}"
            : $"{CostPerUnit:C2} each";
        public string SalePriceDisplay => IsMeasured
            ? $"{SalePrice:C2} / {UnitOfMeasure}"
            : $"{SalePrice:C2} each";
        public string ProfitPerUnitDisplay => IsMeasured
            ? $"{ProfitPerUnit:C2} / {UnitOfMeasure}"
            : $"{ProfitPerUnit:C2} each";
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);
        public bool HasSku => !string.IsNullOrWhiteSpace(Sku);
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
