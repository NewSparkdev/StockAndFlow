using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class SaleDetailsViewModel : ViewModelBase
    {
        private readonly SalesService _salesService;
        private readonly SaleTransaction _transaction;
        private readonly IDialogService _dialogService;
        private readonly IEditorPresenter _editorPresenter;
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
        public ICommand EditCommand { get; }

        public SaleDetailsViewModel(SalesService salesService, SaleTransaction transaction,
            IDialogService dialogService, IEditorPresenter editorPresenter)
        {
            _salesService = salesService;
            _transaction = transaction;
            _dialogService = dialogService;
            _editorPresenter = editorPresenter;
            _saleDate = transaction.SaleDate;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CloseCommand = new RelayCommand(Close);
            EditCommand = new RelayCommand(async () => await EditAsync());
        }

        private async Task EditAsync()
        {
            await _editorPresenter.ShowEditSaleAsync(_transaction);
            // Transaction was mutated in-place; refresh bindings.
            _saleDate = _transaction.SaleDate;
            OnPropertyChanged(nameof(SaleDate));
            OnPropertyChanged(nameof(Transaction));
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
                await _dialogService.ShowAlertAsync(
                    "Error",
                    $"Failed to update sale date:\n\n{ex.Message}");
            }
        }

        private void Close()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
