using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class CalculationService : IDisposable
    {
        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;
        private readonly SalesService _salesService;
        private readonly ExpenseService _expenseService;
        private readonly InventoryAdjustmentService _adjustmentService;
        private readonly ILogger _log;
        private bool _disposed;

        // Store event handler references for proper unsubscription
        private readonly EventHandler _inventoryChangedHandler;
        private readonly EventHandler _saleRecordedHandler;
        private readonly EventHandler _expenseChangedHandler;
        private readonly EventHandler _adjustmentRecordedHandler;

        // PERFORMANCE: Debouncing to prevent excessive recalculations
        private Timer? _debounceTimer;
        private readonly int _debounceDelayMs = 500; // Wait 500ms after last change before recalculating
        private readonly object _debounceLock = new object();

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
            _log = Log.ForContext<CalculationService>();

            // Create handler references that we can unsubscribe later
            // PERFORMANCE: Use debounced recalculation to prevent excessive calculations
            _inventoryChangedHandler = (s, e) => DebouncedRecalculateMetrics();
            _saleRecordedHandler = (s, e) => DebouncedRecalculateMetrics();
            _expenseChangedHandler = (s, e) => DebouncedRecalculateMetrics();
            _adjustmentRecordedHandler = (s, e) => DebouncedRecalculateMetrics();

            // Subscribe to data changes
            _inventoryService.InventoryChanged += _inventoryChangedHandler;
            _salesService.SaleRecorded += _saleRecordedHandler;
            _expenseService.ExpenseChanged += _expenseChangedHandler;
            _adjustmentService.AdjustmentRecorded += _adjustmentRecordedHandler;

            _log.Debug("CalculationService initialized with event subscriptions and debouncing");
        }

        /// <summary>
        /// Debounced metrics recalculation. Waits for 500ms of inactivity before recalculating.
        /// This prevents excessive calculations during bulk operations (e.g., importing 100 items).
        /// </summary>
        private void DebouncedRecalculateMetrics()
        {
            lock (_debounceLock)
            {
                // Cancel any pending recalculation
                _debounceTimer?.Dispose();

                // Schedule a new recalculation after the debounce delay
                _debounceTimer = new Timer(
                    callback: async _ =>
                    {
                        try
                        {
                            _log.Debug("Debounced recalculation triggered");
                            await RecalculateMetricsAsync();
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, "Error during debounced metrics recalculation");
                        }
                    },
                    state: null,
                    dueTime: _debounceDelayMs,
                    period: Timeout.Infinite);
            }
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
            // PERFORMANCE: Uses database-level filtering for large datasets
            var sales = periodStart.HasValue && periodEnd.HasValue
                ? await _salesService.GetSalesByDateRangeAsync(periodStart.Value, periodEnd.Value)
                : await _salesService.GetAllSalesAsync();

            metrics.TotalRevenue = sales.Sum(s => s.Revenue);
            metrics.TotalCOGS = sales.Sum(s => s.COGS);
            metrics.GrossProfit = metrics.TotalRevenue - metrics.TotalCOGS;
            metrics.TotalSalesCount = sales.Count;
            metrics.TotalItemsSold = sales.Sum(s => s.Quantity);

            // Get expenses (filtered by period if specified)
            // PERFORMANCE: Uses database-level filtering for large datasets
            var expenses = periodStart.HasValue && periodEnd.HasValue
                ? await _expenseService.GetExpensesByDateRangeAsync(periodStart.Value, periodEnd.Value)
                : await _expenseService.GetAllExpensesAsync();

            metrics.TotalExpenses = expenses.Sum(e => e.Amount);

            // Get inventory adjustments (filtered by period if specified)
            // PERFORMANCE: Uses database-level filtering for large datasets
            var adjustments = periodStart.HasValue && periodEnd.HasValue
                ? await _adjustmentService.GetAdjustmentsByDateRangeAsync(periodStart.Value, periodEnd.Value)
                : await _adjustmentService.GetAllAdjustmentsAsync();

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

        public void Dispose()
        {
            if (_disposed)
                return;

            _log.Debug("Disposing CalculationService and unsubscribing from events");

            // Dispose debounce timer
            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            // Unsubscribe from all events to prevent memory leaks
            _inventoryService.InventoryChanged -= _inventoryChangedHandler;
            _salesService.SaleRecorded -= _saleRecordedHandler;
            _expenseService.ExpenseChanged -= _expenseChangedHandler;
            _adjustmentService.AdjustmentRecorded -= _adjustmentRecordedHandler;

            _disposed = true;
        }
    }
}
