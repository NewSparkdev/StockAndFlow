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
    public class AdjustmentsViewModel : ViewModelBase
    {
        private readonly InventoryAdjustmentService _adjustmentService;
        private readonly InventoryService _inventoryService;

        private ObservableCollection<InventoryAdjustment> _adjustments = new();
        private InventoryAdjustment? _selectedAdjustment;
        private DateTime _startDate = DateTime.Now.AddMonths(-1).Date;
        private DateTime _endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1);

        public ObservableCollection<InventoryAdjustment> Adjustments
        {
            get => _adjustments;
            set
            {
                if (SetProperty(ref _adjustments, value))
                {
                    OnPropertyChanged(nameof(TotalAdjustments));
                    OnPropertyChanged(nameof(TotalReductions));
                    OnPropertyChanged(nameof(TotalAdditions));
                    OnPropertyChanged(nameof(TotalCostLost));
                    OnPropertyChanged(nameof(TotalRevenueLost));
                    OnPropertyChanged(nameof(TotalProfitLost));
                }
            }
        }

        public InventoryAdjustment? SelectedAdjustment
        {
            get => _selectedAdjustment;
            set => SetProperty(ref _selectedAdjustment, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                var normalizedValue = value.Date;
                if (SetProperty(ref _startDate, normalizedValue))
                {
                    _ = FilterAdjustmentsAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                var normalizedValue = value.Date.AddDays(1).AddTicks(-1);
                if (SetProperty(ref _endDate, normalizedValue))
                {
                    _ = FilterAdjustmentsAsync();
                }
            }
        }

        public int TotalAdjustments => Adjustments?.Count ?? 0;
        public int TotalReductions => Adjustments?.Where(a => a.QuantityChange < 0).Sum(a => Math.Abs(a.QuantityChange)) ?? 0;
        public int TotalAdditions => Adjustments?.Where(a => a.QuantityChange > 0).Sum(a => a.QuantityChange) ?? 0;

        // Financial loss properties
        public decimal TotalCostLost => Adjustments?.Where(a => a.IsLoss).Sum(a => a.TotalCost) ?? 0;
        public decimal TotalRevenueLost => Adjustments?.Where(a => a.IsLoss).Sum(a => a.PotentialRevenue) ?? 0;
        public decimal TotalProfitLost => Adjustments?.Where(a => a.IsLoss).Sum(a => a.PotentialProfit) ?? 0;

        public ICommand RecordAdjustmentCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand DeleteAdjustmentCommand { get; }
        public ICommand RefreshCommand { get; }

        public AdjustmentsViewModel(
            InventoryAdjustmentService adjustmentService,
            InventoryService inventoryService)
        {
            _adjustmentService = adjustmentService;
            _inventoryService = inventoryService;

            RecordAdjustmentCommand = new RelayCommand(RecordAdjustment);
            ViewDetailsCommand = new RelayCommand(ViewDetails, () => SelectedAdjustment != null);
            DeleteAdjustmentCommand = new RelayCommand(async () => await DeleteAdjustmentAsync(), () => SelectedAdjustment != null);
            RefreshCommand = new RelayCommand(async () => await FilterAdjustmentsAsync());

            _adjustmentService.AdjustmentRecorded += (s, e) => _ = FilterAdjustmentsAsync();

            _ = FilterAdjustmentsAsync();
        }

        private async Task FilterAdjustmentsAsync()
        {
            var adjustments = await _adjustmentService.GetAdjustmentsByDateRangeAsync(StartDate, EndDate);
            Adjustments = new ObservableCollection<InventoryAdjustment>(adjustments);
        }

        private void RecordAdjustment()
        {
            var viewModel = new RecordAdjustmentViewModel(_adjustmentService, _inventoryService);
            var dialog = new Views.Dialogs.RecordAdjustmentDialog(viewModel);
            dialog.ShowDialog();
        }

        private void ViewDetails()
        {
            if (SelectedAdjustment == null)
                return;

            var viewModel = new AdjustmentDetailsViewModel(_adjustmentService, _inventoryService, SelectedAdjustment);
            var dialog = new Views.Dialogs.AdjustmentDetailsDialog(viewModel);
            dialog.ShowDialog();
        }

        private async Task DeleteAdjustmentAsync()
        {
            if (SelectedAdjustment == null)
                return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete this adjustment?\\n\\nItem: {SelectedAdjustment.InventoryItemName}\\nReason: {SelectedAdjustment.ReasonDisplay}\\nChange: {SelectedAdjustment.QuantityChangeDisplay}\\n\\nThis will reverse the inventory change.",
                "Confirm Delete Adjustment",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _adjustmentService.DeleteAdjustmentAsync(SelectedAdjustment.Id);
                SelectedAdjustment = null;
            }
        }
    }
}
