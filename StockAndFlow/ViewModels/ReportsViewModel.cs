using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly InventoryService _inventoryService;
        private readonly ExpenseService _expenseService;
        private readonly InventoryAdjustmentService _adjustmentService;
        private readonly ExportImportService _exportImportService;

        private DateTime _startDate = DateTime.Now.AddYears(-5).Date;
        private DateTime _endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1);
        private bool _isLoading;

        // Sales Summary
        private int _totalSalesCount;
        private decimal _totalRevenue;
        private decimal _totalCOGS;
        private decimal _totalSalesProfit;
        private decimal _averageSaleValue;

        // Expenses
        private int _totalExpensesCount;
        private decimal _totalExpenses;
        private ObservableCollection<ExpenseCategoryReport> _expensesByCategory = new();

        // Top Items
        private ObservableCollection<TopSellingItem> _topSellingItems = new();

        // Inventory
        private ObservableCollection<InventoryItem> _lowStockItems = new();
        private decimal _totalInventoryValue;
        private int _totalItemsInStock;

        // Inventory Adjustments & Losses
        private int _totalAdjustments;
        private decimal _totalInventoryLosses;
        private decimal _totalCostLost;
        private decimal _totalProfitLost;

        // Profit & Loss
        private decimal _netProfit;
        private decimal _profitMargin;

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    _ = GenerateReportsAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    _ = GenerateReportsAsync();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // Sales Summary Properties
        public int TotalSalesCount
        {
            get => _totalSalesCount;
            set => SetProperty(ref _totalSalesCount, value);
        }

        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => SetProperty(ref _totalRevenue, value);
        }

        public decimal TotalCOGS
        {
            get => _totalCOGS;
            set => SetProperty(ref _totalCOGS, value);
        }

        public decimal TotalSalesProfit
        {
            get => _totalSalesProfit;
            set => SetProperty(ref _totalSalesProfit, value);
        }

        public decimal AverageSaleValue
        {
            get => _averageSaleValue;
            set => SetProperty(ref _averageSaleValue, value);
        }

        // Expense Properties
        public int TotalExpensesCount
        {
            get => _totalExpensesCount;
            set => SetProperty(ref _totalExpensesCount, value);
        }

        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set => SetProperty(ref _totalExpenses, value);
        }

        public ObservableCollection<ExpenseCategoryReport> ExpensesByCategory
        {
            get => _expensesByCategory;
            set => SetProperty(ref _expensesByCategory, value);
        }

        // Top Items Properties
        public ObservableCollection<TopSellingItem> TopSellingItems
        {
            get => _topSellingItems;
            set => SetProperty(ref _topSellingItems, value);
        }

        // Inventory Properties
        public ObservableCollection<InventoryItem> LowStockItems
        {
            get => _lowStockItems;
            set => SetProperty(ref _lowStockItems, value);
        }

        public decimal TotalInventoryValue
        {
            get => _totalInventoryValue;
            set => SetProperty(ref _totalInventoryValue, value);
        }

        public int TotalItemsInStock
        {
            get => _totalItemsInStock;
            set => SetProperty(ref _totalItemsInStock, value);
        }

        // Inventory Adjustment & Loss Properties
        public int TotalAdjustments
        {
            get => _totalAdjustments;
            set => SetProperty(ref _totalAdjustments, value);
        }

        public decimal TotalInventoryLosses
        {
            get => _totalInventoryLosses;
            set => SetProperty(ref _totalInventoryLosses, value);
        }

        public decimal TotalCostLost
        {
            get => _totalCostLost;
            set => SetProperty(ref _totalCostLost, value);
        }

        public decimal TotalProfitLost
        {
            get => _totalProfitLost;
            set => SetProperty(ref _totalProfitLost, value);
        }

        // Profit & Loss Properties
        public decimal NetProfit
        {
            get => _netProfit;
            set => SetProperty(ref _netProfit, value);
        }

        public decimal ProfitMargin
        {
            get => _profitMargin;
            set => SetProperty(ref _profitMargin, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportImportCommand { get; }

        public ReportsViewModel(
            SalesService salesService,
            InventoryService inventoryService,
            ExpenseService expenseService,
            InventoryAdjustmentService adjustmentService,
            ExportImportService exportImportService)
        {
            _salesService = salesService;
            _inventoryService = inventoryService;
            _expenseService = expenseService;
            _adjustmentService = adjustmentService;
            _exportImportService = exportImportService;

            RefreshCommand = new RelayCommand(async () => await GenerateReportsAsync());
            ExportImportCommand = new RelayCommand(OpenExportImportDialog);

            // Subscribe to changes
            _salesService.SaleRecorded += (s, e) => _ = GenerateReportsAsync();
            _inventoryService.InventoryChanged += (s, e) => _ = GenerateReportsAsync();
            _expenseService.ExpenseChanged += (s, e) => _ = GenerateReportsAsync();
            _adjustmentService.AdjustmentRecorded += (s, e) => _ = GenerateReportsAsync();

            _ = GenerateReportsAsync();
        }

        private void OpenExportImportDialog()
        {
            var dialog = new Views.Dialogs.ExportImportDialog(_exportImportService);
            if (dialog.ShowDialog() == true)
            {
                // Refresh reports after import
                _ = GenerateReportsAsync();
            }
        }

        private async Task GenerateReportsAsync()
        {
            IsLoading = true;

            try
            {
                // Get data
                var sales = await _salesService.GetSalesByDateRangeAsync(StartDate, EndDate);
                var expenses = await _expenseService.GetExpensesByDateRangeAsync(StartDate, EndDate);
                var adjustments = await _adjustmentService.GetAdjustmentsByDateRangeAsync(StartDate, EndDate);
                var inventory = await _inventoryService.GetAllItemsAsync();

                // Sales Summary - Count unique transactions, not individual sale records
                TotalSalesCount = sales.Select(s => s.TransactionId).Distinct().Count();
                TotalRevenue = sales.Sum(s => s.Revenue);
                TotalCOGS = sales.Sum(s => s.COGS);
                TotalSalesProfit = sales.Sum(s => s.Profit);
                AverageSaleValue = TotalSalesCount > 0 ? TotalRevenue / TotalSalesCount : 0;

                // Expenses
                TotalExpensesCount = expenses.Count;
                TotalExpenses = expenses.Sum(e => e.Amount);

                var expenseGroups = expenses
                    .GroupBy(e => e.Category)
                    .Select(g => new ExpenseCategoryReport
                    {
                        Category = g.Key,
                        TotalAmount = g.Sum(e => e.Amount),
                        Count = g.Count(),
                        Percentage = TotalExpenses > 0 ? (g.Sum(e => e.Amount) / TotalExpenses) * 100 : 0
                    })
                    .OrderByDescending(e => e.TotalAmount)
                    .ToList();

                ExpensesByCategory = new ObservableCollection<ExpenseCategoryReport>(expenseGroups);

                // Top Selling Items
                var topItems = sales
                    .GroupBy(s => new { s.InventoryItemId, s.ItemName })
                    .Select(g => new TopSellingItem
                    {
                        ItemName = g.Key.ItemName,
                        QuantitySold = g.Sum(s => s.Quantity),
                        Revenue = g.Sum(s => s.Revenue),
                        Profit = g.Sum(s => s.Profit),
                        SalesCount = g.Count()
                    })
                    .OrderByDescending(i => i.Revenue)
                    .Take(10)
                    .ToList();

                TopSellingItems = new ObservableCollection<TopSellingItem>(topItems);

                // Inventory Status
                var lowStock = inventory
                    .Where(i => i.QuantityOnHand <= 10)
                    .OrderBy(i => i.QuantityOnHand)
                    .ToList();

                LowStockItems = new ObservableCollection<InventoryItem>(lowStock);
                TotalInventoryValue = inventory.Sum(i => i.TotalValue);
                TotalItemsInStock = inventory.Sum(i => i.QuantityOnHand);

                // Inventory Adjustments & Losses
                TotalAdjustments = adjustments.Count;
                var losses = adjustments.Where(a => a.IsLoss).ToList();
                TotalInventoryLosses = losses.Sum(a => Math.Abs(a.QuantityChange));
                TotalCostLost = losses.Sum(a => a.TotalCost);
                TotalProfitLost = losses.Sum(a => a.PotentialProfit);

                // Profit & Loss (including inventory losses as a cost)
                NetProfit = TotalSalesProfit - TotalExpenses - TotalCostLost;
                ProfitMargin = TotalRevenue > 0 ? (NetProfit / TotalRevenue) * 100 : 0;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class ExpenseCategoryReport
    {
        public string Category { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class TopSellingItem
    {
        public string ItemName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public int SalesCount { get; set; }
    }
}
