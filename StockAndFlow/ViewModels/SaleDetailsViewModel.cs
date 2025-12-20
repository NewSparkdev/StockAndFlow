using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class SaleDetailsViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly SaleTransaction _transaction;
        private DateTime _saleDate;

        public event EventHandler<bool>? CloseRequested;

        public SaleTransaction Transaction => _transaction;

        public DateTime SaleDate
        {
            get => _saleDate;
            set => SetProperty(ref _saleDate, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public SaleDetailsViewModel(SalesService salesService, SaleTransaction transaction)
        {
            _salesService = salesService;
            _transaction = transaction;
            _saleDate = transaction.SaleDate;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CloseCommand = new RelayCommand(Close);
        }

        private async Task SaveAsync()
        {
            try
            {
                // Update the sale date for all items in this transaction
                foreach (var item in _transaction.Items)
                {
                    var sale = await _salesService.GetSaleByIdAsync(item.Id);
                    if (sale != null)
                    {
                        sale.SaleDate = SaleDate;
                        await _salesService.UpdateSaleAsync(sale);
                    }
                }

                // Update the transaction's date
                _transaction.SaleDate = SaleDate;

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating sale date: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to update sale date:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void Close()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
