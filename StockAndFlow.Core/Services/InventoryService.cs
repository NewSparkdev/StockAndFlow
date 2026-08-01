using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class InventoryService
    {
        private readonly IDataService _dataService;

        public event EventHandler? InventoryChanged;
        public event EventHandler<InventoryItem>? LowStockDetected;

        public InventoryService(IDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task<List<InventoryItem>> GetAllItemsAsync()
        {
            return await _dataService.GetAllAsync<InventoryItem>();
        }

        public async Task<InventoryItem?> GetItemByIdAsync(Guid id)
        {
            return await _dataService.GetByIdAsync<InventoryItem>(id);
        }

        public async Task<InventoryItem> CreateOrUpdateItemAsync(InventoryItem item)
        {
            item.LastModifiedDate = DateTime.Now;
            await _dataService.SaveAsync(item);
            InventoryChanged?.Invoke(this, EventArgs.Empty);
            return item;
        }

        /// <summary>
        /// Soft deletes an inventory item. The item is marked as deleted but not physically removed.
        /// This prevents data integrity issues with related sales, expenses, and adjustments.
        /// </summary>
        public async Task DeleteItemAsync(Guid id)
        {
            var item = await GetItemByIdAsync(id);
            if (item == null)
            {
                throw new InvalidOperationException($"Inventory item with ID {id} not found");
            }

            // Check if item can be deleted (no dependent records)
            // Foreign key constraints will prevent deletion if there are sales/adjustments
            // But we'll do a soft delete instead to preserve history

            item.IsDeleted = true;
            item.DeletedDate = DateTime.Now;
            item.LastModifiedDate = DateTime.Now;

            await _dataService.SaveAsync(item);

            Log.Information("Inventory item soft-deleted: {ItemId} - {ItemName}", id, item.Name);
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Permanently deletes an inventory item from the database.
        /// Use with caution - this will fail if there are related sales/adjustments due to FK constraints.
        /// </summary>
        public async Task HardDeleteItemAsync(Guid id)
        {
            try
            {
                await _dataService.DeleteAsync<InventoryItem>(id);
                Log.Warning("Inventory item hard-deleted: {ItemId}", id);
                InventoryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to hard delete inventory item {ItemId}. This may be due to related sales/adjustments.", id);
                throw new InvalidOperationException(
                    "Cannot delete this item because it has related sales, expenses, or adjustments. " +
                    "Use soft delete instead to preserve historical data.", ex);
            }
        }

        /// <summary>
        /// Restores a soft-deleted inventory item.
        /// </summary>
        public async Task RestoreItemAsync(Guid id)
        {
            // Need to query with IgnoreQueryFilters to get deleted items
            var item = await _dataService.GetByIdAsync<InventoryItem>(id);
            if (item == null)
            {
                throw new InvalidOperationException($"Inventory item with ID {id} not found");
            }

            if (!item.IsDeleted)
            {
                throw new InvalidOperationException($"Item '{item.Name}' is not deleted");
            }

            item.IsDeleted = false;
            item.DeletedDate = null;
            item.LastModifiedDate = DateTime.Now;

            await _dataService.SaveAsync(item);

            Log.Information("Inventory item restored: {ItemId} - {ItemName}", id, item.Name);
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <param name="forceAllowNegative">
        /// Bypasses the AllowNegativeInventory setting. Used for automatic consequences of a
        /// recorded sale (BOM component deductions, sale-edit deltas): the sale already happened,
        /// so the books must follow it — a negative count tells the user their numbers were off,
        /// whereas skipping the deduction would silently overstate stock.
        /// </param>
        public async Task<bool> AdjustQuantityAsync(Guid itemId, decimal quantityChange, bool forceAllowNegative = false)
        {
            var item = await GetItemByIdAsync(itemId);
            if (item == null)
                return false;

            var newQuantity = item.QuantityOnHand + quantityChange;

            if (newQuantity < 0 && !forceAllowNegative)
            {
                var settings = await _dataService.GetSettingsAsync();
                if (!settings.AllowNegativeInventory)
                    return false;
            }

            item.QuantityOnHand = newQuantity;
            await CreateOrUpdateItemAsync(item);

            // Fire low-stock alert if the item just crossed below its minimum threshold
            if (item.MinimumStockLevel > 0 && item.QuantityOnHand <= item.MinimumStockLevel)
                LowStockDetected?.Invoke(this, item);

            return true;
        }

        public async Task<List<InventoryItem>> GetLowStockItemsAsync(int threshold = 5)
        {
            var items = await GetAllItemsAsync();
            return items.Where(i => i.QuantityOnHand <= threshold).ToList();
        }

        public async Task<List<InventoryItem>> SearchAsync(string searchTerm)
        {
            var items = await GetAllItemsAsync();
            var term = searchTerm.ToLower();

            return items.Where(i =>
                i.Name.ToLower().Contains(term) ||
                (i.Sku?.ToLower().Contains(term) ?? false) ||
                (i.Category?.ToLower().Contains(term) ?? false)
            ).ToList();
        }

        public async Task<List<InventoryItem>> GetDeletedItemsAsync()
        {
            return await _dataService.GetWhereAsync<InventoryItem>(i => i.IsDeleted);
        }
    }
}
