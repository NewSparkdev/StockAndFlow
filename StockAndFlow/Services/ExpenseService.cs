using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class ExpenseService
    {
        private readonly IDataService _dataService;

        public event EventHandler? ExpenseChanged;

        public ExpenseService(IDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task<List<Expense>> GetAllExpensesAsync()
        {
            return await _dataService.GetAllAsync<Expense>();
        }

        public async Task<Expense?> GetExpenseByIdAsync(Guid id)
        {
            return await _dataService.GetByIdAsync<Expense>(id);
        }

        public async Task<Expense> CreateOrUpdateExpenseAsync(Expense expense)
        {
            await _dataService.SaveAsync(expense);
            ExpenseChanged?.Invoke(this, EventArgs.Empty);
            return expense;
        }

        public async Task<Expense> UpdateExpenseAsync(Expense expense)
        {
            await _dataService.SaveAsync(expense);
            ExpenseChanged?.Invoke(this, EventArgs.Empty);
            return expense;
        }

        public async Task DeleteExpenseAsync(Guid id)
        {
            await _dataService.DeleteAsync<Expense>(id);
            ExpenseChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<Expense>> GetExpensesByItemAsync(Guid inventoryItemId)
        {
            var expenses = await GetAllExpensesAsync();
            return expenses.Where(e => e.InventoryItemId == inventoryItemId)
                          .OrderByDescending(e => e.ExpenseDate)
                          .ToList();
        }

        public async Task<List<Expense>> GetExpensesByCategoryAsync(string category)
        {
            var expenses = await GetAllExpensesAsync();
            return expenses.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                          .OrderByDescending(e => e.ExpenseDate)
                          .ToList();
        }

        public async Task<List<Expense>> GetExpensesByDateRangeAsync(DateTime start, DateTime end)
        {
            var expenses = await GetAllExpensesAsync();
            return expenses.Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end)
                          .OrderByDescending(e => e.ExpenseDate)
                          .ToList();
        }

        public async Task<Dictionary<string, decimal>> GetExpensesByCategoryTotalsAsync()
        {
            var expenses = await GetAllExpensesAsync();
            return expenses.GroupBy(e => e.Category)
                          .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        }
    }
}
