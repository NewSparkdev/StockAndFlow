using System;
using System.Linq;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class CalculationService
    {
        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;
        private readonly SalesService _salesService;
        private readonly ExpenseService _expenseService;
        private readonly InventoryAdjustmentService _adjustmentService;

        public event EventHandler<BusinessMetrics>? MetricsUpdated;

        public CalculationService(
            IDataService dataService,
            InventoryService inventoryService,
            SalesService salesService,
            ExpenseService expenseService,
            InventoryAdjustmentService adjustmentService)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
            _salesService = salesService;
            _expenseService = expenseService;
            _adjustmentService = adjustmentService;

            // Subscribe to data changes
            _inventoryService.InventoryChanged += (s, e) => _ = RecalculateMetricsAsync();
            _salesService.SaleRecorded += (s, e) => _ = RecalculateMetricsAsync();
            _expenseService.ExpenseChanged += (s, e) => _ = RecalculateMetricsAsync();
            _adjustmentService.AdjustmentRecorded += (s, e) => _ = RecalculateMetricsAsync();
        }

        public async Task<BusinessMetrics> RecalculateMetricsAsync(
            DateTime? periodStart = null,
            DateTime? periodEnd = null)
        {
            var metrics = new BusinessMetrics
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                CalculatedAt = DateTime.Now
            };

            // Calculate inventory value
            var inventoryItems = await _inventoryService.GetAllItemsAsync();
            metrics.TotalInventoryValue = inventoryItems.Sum(i => i.TotalValue);
            metrics.UniqueInventoryItems = inventoryItems.Count;

            // Get sales (filtered by period if specified)
            var allSales = await _salesService.GetAllSalesAsync();
            var sales = periodStart.HasValue && periodEnd.HasValue
                ? allSales.Where(s => s.SaleDate >= periodStart && s.SaleDate <= periodEnd).ToList()
                : allSales;

            metrics.TotalRevenue = sales.Sum(s => s.Revenue);
            metrics.TotalCOGS = sales.Sum(s => s.COGS);
            metrics.GrossProfit = metrics.TotalRevenue - metrics.TotalCOGS;
            metrics.TotalSalesCount = sales.Count;
            metrics.TotalItemsSold = sales.Sum(s => s.Quantity);

            // Get expenses (filtered by period if specified)
            var allExpenses = await _expenseService.GetAllExpensesAsync();
            var expenses = periodStart.HasValue && periodEnd.HasValue
                ? allExpenses.Where(e => e.ExpenseDate >= periodStart && e.ExpenseDate <= periodEnd).ToList()
                : allExpenses;

            metrics.TotalExpenses = expenses.Sum(e => e.Amount);

            // Get inventory adjustments (filtered by period if specified)
            var allAdjustments = await _adjustmentService.GetAllAdjustmentsAsync();
            var adjustments = periodStart.HasValue && periodEnd.HasValue
                ? allAdjustments.Where(a => a.AdjustmentDate >= periodStart && a.AdjustmentDate <= periodEnd).ToList()
                : allAdjustments;

            // Calculate inventory losses (cost of lost/damaged items)
            var losses = adjustments.Where(a => a.IsLoss).ToList();
            metrics.TotalInventoryLosses = losses.Sum(a => a.TotalCost);

            // Calculate net profit (including inventory losses as a cost)
            metrics.NetProfit = metrics.GrossProfit - metrics.TotalExpenses - metrics.TotalInventoryLosses;

            // Calculate overall profit margin
            metrics.OverallProfitMargin = metrics.TotalRevenue > 0
                ? (metrics.NetProfit / metrics.TotalRevenue) * 100
                : 0;

            MetricsUpdated?.Invoke(this, metrics);
            return metrics;
        }

        public async Task<decimal> GetItemProfitabilityAsync(Guid inventoryItemId)
        {
            var sales = await _salesService.GetSalesByItemAsync(inventoryItemId);
            var totalProfit = sales.Sum(s => s.Profit);

            var expenses = await _expenseService.GetExpensesByItemAsync(inventoryItemId);
            var totalExpenses = expenses.Sum(e => e.Amount);

            return totalProfit - totalExpenses;
        }
    }
}
