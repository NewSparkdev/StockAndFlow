using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class ExpenseDetailsViewModel : ViewModelBase
    {
        private readonly ExpenseService _expenseService;
        private readonly Expense _expense;
        private readonly IDialogService _dialogService;
        private DateTime _expenseDate;

        public event EventHandler<bool>? CloseRequested;

        public Expense Expense => _expense;

        public DateTime ExpenseDate
        {
            get => _expenseDate;
            set => SetProperty(ref _expenseDate, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public ExpenseDetailsViewModel(ExpenseService expenseService, Expense expense, IDialogService dialogService)
        {
            _expenseService = expenseService;
            _expense = expense;
            _dialogService = dialogService;
            _expenseDate = expense.ExpenseDate;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CloseCommand = new RelayCommand(Close);
        }

        private async Task SaveAsync()
        {
            try
            {
                // Update the expense date
                _expense.ExpenseDate = ExpenseDate;
                await _expenseService.UpdateExpenseAsync(_expense);

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating expense date: {ex.Message}");
                await _dialogService.ShowAlertAsync(
                    "Error",
                    $"Failed to update expense date:\n\n{ex.Message}");
            }
        }

        private void Close()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
