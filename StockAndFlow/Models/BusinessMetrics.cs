using System;

namespace StockAndFlow.Models
{
    public class BusinessMetrics
    {
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCOGS { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalInventoryLosses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal OverallProfitMargin { get; set; }

        public int TotalSalesCount { get; set; }
        public int TotalItemsSold { get; set; }
        public int UniqueInventoryItems { get; set; }

        public DateTime CalculatedAt { get; set; }

        // Period-specific metrics (optional)
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
    }
}
