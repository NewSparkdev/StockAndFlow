using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class AddEditExpenseViewModel : ViewModelBase
    {
        private readonly ExpenseService _expenseService;
        private readonly InventoryService _inventoryService;
        private readonly IFilePickerService _filePicker;
        private readonly IDialogService _dialogService;
        private Expense? _expenseToEdit;

        private DateTime _expenseDate = DateTime.Now;
        private decimal _amount;
        private string _category = string.Empty;
        private string? _description;
        private Guid? _selectedInventoryItemId;
        private string _receiptImagePath = string.Empty;

        public event EventHandler<bool>? CloseRequested;

        // Common expense categories
        public List<string> AvailableCategories { get; } = new()
        {
            "Rent & Utilities",
            "Salaries & Wages",
            "Marketing & Advertising",
            "Office Supplies",
            "Equipment",
            "Inventory Purchase",
            "Shipping & Delivery",
            "Professional Services",
            "Insurance",
            "Taxes & Licenses",
            "Maintenance & Repairs",
            "Travel & Entertainment",
            "Software & Subscriptions",
            "Miscellaneous"
        };

        private List<InventoryItem> _availableInventoryItems = new();
        public List<InventoryItem> AvailableInventoryItems
        {
            get => _availableInventoryItems;
            set => SetProperty(ref _availableInventoryItems, value);
        }

        public bool IsEditMode => _expenseToEdit != null;

        public string DialogTitle => IsEditMode ? "Edit Expense" : "Add New Expense";

        public DateTime ExpenseDate
        {
            get => _expenseDate;
            set
            {
                if (SetProperty(ref _expenseDate, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string Category
        {
            get => _category;
            set
            {
                if (SetProperty(ref _category, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public Guid? SelectedInventoryItemId
        {
            get => _selectedInventoryItemId;
            set => SetProperty(ref _selectedInventoryItemId, value);
        }

        public string ReceiptImagePath
        {
            get => _receiptImagePath;
            set
            {
                if (SetProperty(ref _receiptImagePath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasReceipt));
                }
            }
        }

        public bool HasReceipt => !string.IsNullOrEmpty(ReceiptImagePath);

        public bool IsValid =>
            Amount > 0 &&
            !string.IsNullOrWhiteSpace(Category);

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseReceiptCommand { get; }

        public AddEditExpenseViewModel(ExpenseService expenseService, InventoryService inventoryService, IFilePickerService filePicker, IDialogService dialogService)
        {
            _expenseService = expenseService;
            _inventoryService = inventoryService;
            _filePicker = filePicker;
            _dialogService = dialogService;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsValid);
            CancelCommand = new RelayCommand(Cancel);
            BrowseReceiptCommand = new RelayCommand(async () => await BrowseReceiptAsync());

            _ = LoadInventoryItemsAsync();
        }

        public void LoadExpense(Expense expense)
        {
            _expenseToEdit = expense;
            ExpenseDate = expense.ExpenseDate;
            Amount = expense.Amount;
            Category = expense.Category;
            Description = expense.Description;
            SelectedInventoryItemId = expense.InventoryItemId;
            ReceiptImagePath = expense.ReceiptImagePath;

            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(DialogTitle));
        }

        private async Task LoadInventoryItemsAsync()
        {
            try
            {
                var items = await _inventoryService.GetAllItemsAsync();
                AvailableInventoryItems = items.OrderBy(i => i.Name).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading inventory items: {ex.Message}");
            }
        }

        private async Task BrowseReceiptAsync()
        {
            var stored = await _filePicker.PickAndStoreImageAsync("Select Receipt Image", "receipt_");
            if (stored != null)
            {
                ReceiptImagePath = stored;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                if (IsEditMode && _expenseToEdit != null)
                {
                    // Update existing expense
                    _expenseToEdit.ExpenseDate = ExpenseDate;
                    _expenseToEdit.Amount = Amount;
                    _expenseToEdit.Category = Category;
                    _expenseToEdit.Description = Description;
                    _expenseToEdit.InventoryItemId = SelectedInventoryItemId;
                    _expenseToEdit.ReceiptImagePath = ReceiptImagePath;

                    await _expenseService.CreateOrUpdateExpenseAsync(_expenseToEdit);
                }
                else
                {
                    // Add new expense
                    var expense = new Expense
                    {
                        ExpenseDate = ExpenseDate,
                        Amount = Amount,
                        Category = Category,
                        Description = Description,
                        InventoryItemId = SelectedInventoryItemId,
                        ReceiptImagePath = ReceiptImagePath
                    };

                    await _expenseService.CreateOrUpdateExpenseAsync(expense);
                }

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving expense: {ex.Message}");
                await _dialogService.ShowAlertAsync("Save failed", $"Could not save the expense:\n\n{ex.Message}");
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
