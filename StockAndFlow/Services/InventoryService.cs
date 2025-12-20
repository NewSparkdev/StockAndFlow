using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class InventoryService
    {
        private readonly IDataService _dataService;

        public event EventHandler? InventoryChanged;

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

        public async Task DeleteItemAsync(Guid id)
        {
            await _dataService.DeleteAsync<InventoryItem>(id);
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> AdjustQuantityAsync(Guid itemId, int quantityChange)
        {
            var item = await GetItemByIdAsync(itemId);
            if (item == null)
                return false;

            var newQuantity = item.QuantityOnHand + quantityChange;

            var settings = await _dataService.GetSettingsAsync();
            if (newQuantity < 0 && !settings.AllowNegativeInventory)
                return false;

            item.QuantityOnHand = newQuantity;
            await CreateOrUpdateItemAsync(item);
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
    }
}
