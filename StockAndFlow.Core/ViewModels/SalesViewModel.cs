using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class SalesViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly InventoryService _inventoryService;
        private readonly BusinessSettingsService _businessSettingsService;
        private readonly InvoiceService _invoiceService;
        private readonly IDialogService _dialogService;
        private readonly IEditorPresenter _editorPresenter;

        private ObservableCollection<SaleTransaction> _sales = new();
        private SaleTransaction? _selectedSale;
        private bool _isLoading;
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

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
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
        public ICommand EditSaleCommand { get; }
        public ICommand DeleteSaleCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand RefreshCommand { get; }

        public SalesViewModel(SalesService salesService, InventoryService inventoryService,
            BusinessSettingsService businessSettingsService, InvoiceService invoiceService,
            IDialogService dialogService, IEditorPresenter editorPresenter)
        {
            _salesService = salesService;
            _inventoryService = inventoryService;
            _businessSettingsService = businessSettingsService;
            _invoiceService = invoiceService;
            _dialogService = dialogService;
            _editorPresenter = editorPresenter;

            RecordSaleCommand = new RelayCommand(async () => await RecordSaleAsync());
            EditSaleCommand = new RelayCommand(async () => await EditSaleAsync(), () => SelectedSale != null);
            DeleteSaleCommand = new RelayCommand(async () => await DeleteSaleAsync(), () => SelectedSale != null);
            ViewDetailsCommand = new RelayCommand(async () => await ViewDetailsAsync(), () => SelectedSale != null);
            RefreshCommand = new RelayCommand(async () => await FilterSalesAsync());

            _salesService.SaleRecorded += (s, e) => _ = FilterSalesAsync();

            _ = FilterSalesAsync();
        }

        private async Task LoadSalesAsync()
        {
            IsLoading = true;
            try
            {
                var sales = await _salesService.GetAllSalesAsync();
                var transactions = GroupSalesByTransaction(sales);
                UiDispatcher.Run(() => Sales = new ObservableCollection<SaleTransaction>(transactions.OrderByDescending(t => t.SaleDate)));
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to load sales: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task FilterSalesAsync()
        {
            IsLoading = true;
            try
            {
                var sales = await _salesService.GetSalesByDateRangeAsync(StartDate, EndDate);
                var transactions = GroupSalesByTransaction(sales);
                UiDispatcher.Run(() => Sales = new ObservableCollection<SaleTransaction>(transactions));
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to load sales: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
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

        private async Task RecordSaleAsync()
        {
            await _editorPresenter.ShowRecordSaleAsync();
        }

        private async Task EditSaleAsync()
        {
            if (SelectedSale == null)
                return;
            await _editorPresenter.ShowEditSaleAsync(SelectedSale);
        }

        private async Task ViewDetailsAsync()
        {
            if (SelectedSale == null)
                return;

            try
            {
                await _editorPresenter.ShowSaleDetailsAsync(SelectedSale);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to open sale details");
                await _dialogService.ShowAlertAsync("Error", "Could not open sale details. Please try again.");
            }
        }

        private async Task DeleteSaleAsync()
        {
            if (SelectedSale == null)
                return;

            var itemsList = string.Join("\n", SelectedSale.Items.Select(i => $"  • {i.Quantity}x {i.ItemName}"));

            if (await _dialogService.ShowConfirmAsync(
                "Confirm Delete Sale",
                $"Are you sure you want to delete this sale?\n\nItems:\n{itemsList}\n\nTotal Revenue: {SelectedSale.Revenue:C2}\nTotal Items: {SelectedSale.ItemCount}\n\nThis will restore all items to inventory.\nThis action cannot be undone."))
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
