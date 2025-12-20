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
    public class SalesViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly InventoryService _inventoryService;
        private readonly BusinessSettingsService _businessSettingsService;
        private readonly InvoiceService _invoiceService;

        private ObservableCollection<SaleTransaction> _sales = new();
        private SaleTransaction? _selectedSale;
        private DateTime _startDate = DateTime.Now.AddMonths(-1).Date; // Start of day
        private DateTime _endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1); // End of day

        public ObservableCollection<SaleTransaction> Sales
        {
            get => _sales;
            set
            {
                if (SetProperty(ref _sales, value))
                {
                    OnPropertyChanged(nameof(TotalRevenue));
                }
            }
        }

        public SaleTransaction? SelectedSale
        {
            get => _selectedSale;
            set => SetProperty(ref _selectedSale, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                // Normalize to start of day (00:00:00)
                var normalizedValue = value.Date;
                if (SetProperty(ref _startDate, normalizedValue))
                {
                    _ = FilterSalesAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                // Normalize to end of day (23:59:59.9999999)
                var normalizedValue = value.Date.AddDays(1).AddTicks(-1);
                if (SetProperty(ref _endDate, normalizedValue))
                {
                    _ = FilterSalesAsync();
                }
            }
        }


        public decimal TotalRevenue => Sales?.Sum(s => s.Revenue) ?? 0;

        public ICommand RecordSaleCommand { get; }
        public ICommand DeleteSaleCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand RefreshCommand { get; }

        public SalesViewModel(SalesService salesService, InventoryService inventoryService,
            BusinessSettingsService businessSettingsService, InvoiceService invoiceService)
        {
            _salesService = salesService;
            _inventoryService = inventoryService;
            _businessSettingsService = businessSettingsService;
            _invoiceService = invoiceService;

            RecordSaleCommand = new RelayCommand(RecordSale);
            DeleteSaleCommand = new RelayCommand(async () => await DeleteSaleAsync(), () => SelectedSale != null);
            ViewDetailsCommand = new RelayCommand(ViewDetails, () => SelectedSale != null);
            RefreshCommand = new RelayCommand(async () => await FilterSalesAsync());

            _salesService.SaleRecorded += (s, e) => _ = FilterSalesAsync();

            _ = FilterSalesAsync();
        }

        private async Task LoadSalesAsync()
        {
            var sales = await _salesService.GetAllSalesAsync();
            var transactions = GroupSalesByTransaction(sales);
            Sales = new ObservableCollection<SaleTransaction>(transactions.OrderByDescending(t => t.SaleDate));
        }

        private async Task FilterSalesAsync()
        {
            var sales = await _salesService.GetSalesByDateRangeAsync(StartDate, EndDate);
            var transactions = GroupSalesByTransaction(sales);
            Sales = new ObservableCollection<SaleTransaction>(transactions);
        }

        private List<SaleTransaction> GroupSalesByTransaction(List<Sale> sales)
        {
            return sales
                .GroupBy(s => s.TransactionId)
                .Select(g => new SaleTransaction
                {
                    TransactionId = g.Key,
                    SaleDate = g.First().SaleDate,
                    CustomerName = g.First().CustomerName,
                    CustomerEmail = g.First().CustomerEmail,
                    Notes = g.First().Notes,
                    Items = g.OrderBy(s => s.ItemName).ToList()
                })
                .ToList();
        }

        private void RecordSale()
        {
            var viewModel = new RecordSaleViewModel(_salesService, _inventoryService,
                _businessSettingsService, _invoiceService);
            var dialog = new Views.Dialogs.RecordSaleDialog(viewModel);
            dialog.ShowDialog();
        }

        private void ViewDetails()
        {
            if (SelectedSale == null)
                return;

            try
            {
                var viewModel = new SaleDetailsViewModel(_salesService, SelectedSale);
                var dialog = new Views.Dialogs.SaleDetailsDialog(viewModel);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error viewing sale details:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task DeleteSaleAsync()
        {
            if (SelectedSale == null)
                return;

            var itemsList = string.Join("\n", SelectedSale.Items.Select(i => $"  • {i.Quantity}x {i.ItemName}"));

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete this sale?\n\nItems:\n{itemsList}\n\nTotal Revenue: {SelectedSale.Revenue:C2}\nTotal Items: {SelectedSale.ItemCount}\n\nThis will restore all items to inventory.\nThis action cannot be undone.",
                "Confirm Delete Sale",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // Delete all items in this transaction
                foreach (var item in SelectedSale.Items)
                {
                    await _salesService.DeleteSaleAsync(item.Id, restoreInventory: true);
                }
                SelectedSale = null;
            }
        }
    }
}
