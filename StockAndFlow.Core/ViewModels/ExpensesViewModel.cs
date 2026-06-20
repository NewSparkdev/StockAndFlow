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
    public class ExpensesViewModel : ViewModelBase
    {
        private readonly ExpenseService _expenseService;
        private readonly InventoryService _inventoryService;
        private readonly IDialogService _dialogService;
        private readonly IEditorPresenter _editorPresenter;

        private ObservableCollection<Expense> _expenses = new();
        private Expense? _selectedExpense;
        private DateTime _startDate = DateTime.Now.AddMonths(-1);
        private DateTime _endDate = DateTime.Now;

        public ObservableCollection<Expense> Expenses
        {
            get => _expenses;
            set
            {
                if (SetProperty(ref _expenses, value))
                {
                    OnPropertyChanged(nameof(TotalExpenses));
                }
            }
        }

        public decimal TotalExpenses => Expenses?.Sum(e => e.Amount) ?? 0;

        public Expense? SelectedExpense
        {
            get => _selectedExpense;
            set => SetProperty(ref _selectedExpense, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    _ = FilterExpensesAsync();
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
                    _ = FilterExpensesAsync();
                }
            }
        }


        public ICommand AddExpenseCommand { get; }
        public ICommand EditExpenseCommand { get; }
        public ICommand DeleteExpenseCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand RefreshCommand { get; }

        public ExpensesViewModel(ExpenseService expenseService, InventoryService inventoryService, IDialogService dialogService, IEditorPresenter editorPresenter)
        {
            _expenseService = expenseService;
            _inventoryService = inventoryService;
            _dialogService = dialogService;
            _editorPresenter = editorPresenter;

            AddExpenseCommand = new RelayCommand(async () => await AddExpenseAsync());
            EditExpenseCommand = new RelayCommand(async () => await EditExpenseAsync(), () => SelectedExpense != null);
            DeleteExpenseCommand = new RelayCommand(async () => await DeleteExpenseAsync(), () => SelectedExpense != null);
            ViewDetailsCommand = new RelayCommand(async () => await ViewDetailsAsync(), () => SelectedExpense != null);
            RefreshCommand = new RelayCommand(async () => await LoadExpensesAsync());

            _expenseService.ExpenseChanged += (s, e) => _ = LoadExpensesAsync();

            _ = LoadExpensesAsync();
        }

        private async Task LoadExpensesAsync()
        {
            var expenses = await _expenseService.GetAllExpensesAsync();
            Expenses = new ObservableCollection<Expense>(expenses.OrderByDescending(e => e.ExpenseDate));
        }

        private async Task FilterExpensesAsync()
        {
            var expenses = await _expenseService.GetExpensesByDateRangeAsync(StartDate, EndDate);
            Expenses = new ObservableCollection<Expense>(expenses);
        }

        private async Task AddExpenseAsync()
        {
            await _editorPresenter.ShowAddExpenseAsync();
        }

        private async Task EditExpenseAsync()
        {
            if (SelectedExpense == null)
                return;

            await _editorPresenter.ShowEditExpenseAsync(SelectedExpense);
        }

        private async Task ViewDetailsAsync()
        {
            if (SelectedExpense == null)
                return;

            await _editorPresenter.ShowExpenseDetailsAsync(SelectedExpense);
        }

        private async Task DeleteExpenseAsync()
        {
            if (SelectedExpense == null)
                return;

            if (await _dialogService.ShowConfirmAsync(
                "Confirm Delete Expense",
                $"Are you sure you want to delete this expense?\n\nCategory: {SelectedExpense.Category}\nAmount: {SelectedExpense.Amount:C2}\nDate: {SelectedExpense.ExpenseDate:MMM dd, yyyy}\n\nThis action cannot be undone."))
            {
                await _expenseService.DeleteExpenseAsync(SelectedExpense.Id);
                SelectedExpense = null;
            }
        }
    }
}
