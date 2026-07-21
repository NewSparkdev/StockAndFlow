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
        private readonly IEditorPresenter _editorPresenter;
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
        public ICommand EditCommand { get; }

        public ExpenseDetailsViewModel(ExpenseService expenseService, Expense expense, IDialogService dialogService, IEditorPresenter editorPresenter)
        {
            _expenseService = expenseService;
            _expense = expense;
            _dialogService = dialogService;
            _editorPresenter = editorPresenter;
            _expenseDate = expense.ExpenseDate;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CloseCommand = new RelayCommand(Close);
            EditCommand = new RelayCommand(async () => await EditAsync());
        }

        private async Task EditAsync()
        {
            await _editorPresenter.ShowEditExpenseAsync(_expense);
            // The expense object was mutated in-place by the edit form; refresh bindings.
            _expenseDate = _expense.ExpenseDate;
            OnPropertyChanged(nameof(ExpenseDate));
            OnPropertyChanged(nameof(Expense));
        }

        private async Task SaveAsync()
        {
            try
            {
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
