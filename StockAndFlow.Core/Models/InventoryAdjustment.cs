using System;

namespace StockAndFlow.Models
{
    public enum AdjustmentReason
    {
        Damaged,
        CustomerReturn,
        Lost,
        Expired,
        Found,
        Correction,
        Other
    }

    public class InventoryAdjustment
    {
        public Guid Id { get; set; }
        public Guid InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public AdjustmentReason Reason { get; set; }
        public decimal QuantityChange { get; set; } // Negative for reductions, positive for additions
        public decimal CostPerUnit { get; set; }
        public decimal SalePricePerUnit { get; set; }
        public string? Notes { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public DateTime CreatedDate { get; set; }

        // Financial impact properties
        public decimal TotalCost => Math.Abs(QuantityChange) * CostPerUnit;
        public decimal PotentialRevenue => Math.Abs(QuantityChange) * SalePricePerUnit;
        public decimal PotentialProfit => PotentialRevenue - TotalCost;
        public bool IsLoss => QuantityChange < 0;

        // UI Helper properties
        public string ReasonDisplay
        {
            get
            {
                return Reason switch
                {
                    AdjustmentReason.Damaged => "Damaged",
                    AdjustmentReason.CustomerReturn => "Customer Return",
                    AdjustmentReason.Lost => "Lost/Stolen",
                    AdjustmentReason.Expired => "Expired",
                    AdjustmentReason.Found => "Found",
                    AdjustmentReason.Correction => "Inventory Correction",
                    AdjustmentReason.Other => "Other",
                    _ => "Unknown"
                };
            }
        }

        public string QuantityChangeDisplay
        {
            get
            {
                if (QuantityChange > 0)
                    return $"+{QuantityChange:0.###}";
                return QuantityChange.ToString("0.###");
            }
        }

        public string QuantityChangeColor
        {
            get
            {
                if (QuantityChange < 0)
                    return "#E74C3C"; // Red for reductions
                if (QuantityChange > 0)
                    return "#27AE60"; // Green for additions
                return "#95A5A6"; // Gray for no change
            }
        }

        public InventoryAdjustment()
        {
            Id = Guid.NewGuid();
            AdjustmentDate = DateTime.Now;
            CreatedDate = DateTime.Now;
        }
    }
}
