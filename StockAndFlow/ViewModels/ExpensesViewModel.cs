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
    public class ExpensesViewModel : ViewModelBase
    {
        private readonly ExpenseService _expenseService;
        private readonly InventoryService _inventoryService;

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

        public ExpensesViewModel(ExpenseService expenseService, InventoryService inventoryService)
        {
            _expenseService = expenseService;
            _inventoryService = inventoryService;

            AddExpenseCommand = new RelayCommand(AddExpense);
            EditExpenseCommand = new RelayCommand(EditExpense, () => SelectedExpense != null);
            DeleteExpenseCommand = new RelayCommand(async () => await DeleteExpenseAsync(), () => SelectedExpense != null);
            ViewDetailsCommand = new RelayCommand(ViewDetails, () => SelectedExpense != null);
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

        private void AddExpense()
        {
            var viewModel = new AddEditExpenseViewModel(_expenseService, _inventoryService);
            var dialog = new Views.Dialogs.AddEditExpenseDialog(viewModel);
            dialog.ShowDialog();
        }

        private void EditExpense()
        {
            if (SelectedExpense == null)
                return;

            var viewModel = new AddEditExpenseViewModel(_expenseService, _inventoryService);
            viewModel.LoadExpense(SelectedExpense);

            var dialog = new Views.Dialogs.AddEditExpenseDialog(viewModel);
            dialog.ShowDialog();
        }

        private void ViewDetails()
        {
            if (SelectedExpense == null)
                return;

            var viewModel = new ExpenseDetailsViewModel(_expenseService, SelectedExpense);
            var dialog = new Views.Dialogs.ExpenseDetailsDialog(viewModel);
            dialog.ShowDialog();
        }

        private async Task DeleteExpenseAsync()
        {
            if (SelectedExpense == null)
                return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete this expense?\n\nCategory: {SelectedExpense.Category}\nAmount: {SelectedExpense.Amount:C2}\nDate: {SelectedExpense.ExpenseDate:MMM dd, yyyy}\n\nThis action cannot be undone.",
                "Confirm Delete Expense",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _expenseService.DeleteExpenseAsync(SelectedExpense.Id);
                SelectedExpense = null;
            }
        }
    }
}
