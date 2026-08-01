using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
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
        private readonly IEditorPresenter _editorPresenter;

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
        private decimal _totalItemsInStock;

        // Inventory Adjustments & Losses
        private int _totalAdjustments;
        private decimal _totalInventoryLosses;
        private decimal _totalCostLost;
        private decimal _totalProfitLost;

        // Profit & Loss
        private decimal _netProfit;
        private decimal _profitMargin;

        // Chart Data
        private ISeries[] _salesTrendSeries = Array.Empty<ISeries>();
        private Axis[] _salesTrendXAxes = Array.Empty<Axis>();
        private ISeries[] _topItemsSeries = Array.Empty<ISeries>();
        private Axis[] _topItemsXAxes = Array.Empty<Axis>();
        private ISeries[] _expensesPieSeries = Array.Empty<ISeries>();
        private ISeries[] _inventoryStatusSeries = Array.Empty<ISeries>();

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

        public decimal TotalItemsInStock
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

        // Chart Properties
        public ISeries[] SalesTrendSeries
        {
            get => _salesTrendSeries;
            set => SetProperty(ref _salesTrendSeries, value);
        }

        public Axis[] SalesTrendXAxes
        {
            get => _salesTrendXAxes;
            set => SetProperty(ref _salesTrendXAxes, value);
        }

        public ISeries[] TopItemsSeries
        {
            get => _topItemsSeries;
            set => SetProperty(ref _topItemsSeries, value);
        }

        public Axis[] TopItemsXAxes
        {
            get => _topItemsXAxes;
            set => SetProperty(ref _topItemsXAxes, value);
        }

        public ISeries[] ExpensesPieSeries
        {
            get => _expensesPieSeries;
            set => SetProperty(ref _expensesPieSeries, value);
        }

        public ISeries[] InventoryStatusSeries
        {
            get => _inventoryStatusSeries;
            set => SetProperty(ref _inventoryStatusSeries, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportImportCommand { get; }

        public ReportsViewModel(
            SalesService salesService,
            InventoryService inventoryService,
            ExpenseService expenseService,
            InventoryAdjustmentService adjustmentService,
            ExportImportService exportImportService,
            IEditorPresenter editorPresenter)
        {
            _salesService = salesService;
            _inventoryService = inventoryService;
            _expenseService = expenseService;
            _adjustmentService = adjustmentService;
            _exportImportService = exportImportService;
            _editorPresenter = editorPresenter;

            RefreshCommand = new RelayCommand(async () => await GenerateReportsAsync());
            ExportImportCommand = new RelayCommand(async () => await OpenExportImportDialogAsync());

            // Subscribe to changes
            _salesService.SaleRecorded += (s, e) => _ = GenerateReportsAsync();
            _inventoryService.InventoryChanged += (s, e) => _ = GenerateReportsAsync();
            _expenseService.ExpenseChanged += (s, e) => _ = GenerateReportsAsync();
            _adjustmentService.AdjustmentRecorded += (s, e) => _ = GenerateReportsAsync();

            _ = GenerateReportsAsync();
        }

        private async Task OpenExportImportDialogAsync()
        {
            if (await _editorPresenter.ShowExportImportAsync())
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

                // Apply all computed state on the UI thread (this method can be triggered by
                // background service events).
                UiDispatcher.Run(() =>
                {
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

                // Generate Charts
                GenerateSalesTrendChart(sales);
                GenerateTopItemsChart(topItems);
                GenerateExpensesPieChart(expenseGroups);
                GenerateInventoryStatusChart(inventory);
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to generate reports");
            }
            finally
            {
                UiDispatcher.Run(() => IsLoading = false);
            }
        }

        private void GenerateSalesTrendChart(List<Sale> sales)
        {
            // Group sales by date and sum revenue
            var dailySales = sales
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(s => s.Revenue)
                })
                .OrderBy(x => x.Date)
                .ToList();

            if (!dailySales.Any())
            {
                SalesTrendSeries = Array.Empty<ISeries>();
                SalesTrendXAxes = Array.Empty<Axis>();
                return;
            }

            SalesTrendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = dailySales.Select(x => (double)x.Revenue).ToArray(),
                    Name = "Revenue",
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(SKColors.White)
                }
            };

            SalesTrendXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = dailySales.Select(x => x.Date.ToString("MM/dd")).ToArray(),
                    LabelsRotation = 45
                }
            };
        }

        private void GenerateTopItemsChart(List<TopSellingItem> topItems)
        {
            if (!topItems.Any())
            {
                TopItemsSeries = Array.Empty<ISeries>();
                TopItemsXAxes = Array.Empty<Axis>();
                return;
            }

            var top5 = topItems.Take(5).ToList();

            TopItemsSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = top5.Select(x => (double)x.Revenue).ToArray(),
                    Name = "Revenue",
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                    MaxBarWidth = 50
                }
            };

            TopItemsXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = top5.Select(x => x.ItemName.Length > 15
                        ? x.ItemName.Substring(0, 15) + "..."
                        : x.ItemName).ToArray(),
                    LabelsRotation = 45
                }
            };
        }

        private void GenerateExpensesPieChart(List<ExpenseCategoryReport> expenses)
        {
            if (!expenses.Any())
            {
                ExpensesPieSeries = Array.Empty<ISeries>();
                return;
            }

            var colors = new[]
            {
                SKColors.Tomato,
                SKColors.Orange,
                SKColors.Gold,
                SKColors.MediumSeaGreen,
                SKColors.DodgerBlue,
                SKColors.MediumPurple,
                SKColors.DeepPink
            };

            ExpensesPieSeries = expenses.Select((expense, index) => new PieSeries<double>
            {
                Values = new[] { (double)expense.TotalAmount },
                Name = $"{expense.Category} (${expense.TotalAmount:N2})",
                Fill = new SolidColorPaint(colors[index % colors.Length])
            }).ToArray();
        }

        private void GenerateInventoryStatusChart(List<InventoryItem> inventory)
        {
            if (!inventory.Any())
            {
                InventoryStatusSeries = Array.Empty<ISeries>();
                return;
            }

            var inStock = inventory.Count(i => i.IsInStock);
            var lowStock = inventory.Count(i => i.IsLowStock);
            var outOfStock = inventory.Count(i => i.IsOutOfStock);

            InventoryStatusSeries = new ISeries[]
            {
                new PieSeries<int>
                {
                    Values = new[] { inStock },
                    Name = $"In Stock ({inStock})",
                    Fill = new SolidColorPaint(SKColor.Parse("#27AE60"))
                },
                new PieSeries<int>
                {
                    Values = new[] { lowStock },
                    Name = $"Low Stock ({lowStock})",
                    Fill = new SolidColorPaint(SKColor.Parse("#F39C12"))
                },
                new PieSeries<int>
                {
                    Values = new[] { outOfStock },
                    Name = $"Out of Stock ({outOfStock})",
                    Fill = new SolidColorPaint(SKColor.Parse("#E74C3C"))
                }
            };
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
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public int SalesCount { get; set; }
    }
}
