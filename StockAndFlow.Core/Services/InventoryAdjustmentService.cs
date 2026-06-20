using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class InventoryAdjustmentService
    {
        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;

        public event EventHandler? AdjustmentRecorded;

        public InventoryAdjustmentService(IDataService dataService, InventoryService inventoryService)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
        }

        public async Task<InventoryAdjustment> RecordAdjustmentAsync(InventoryAdjustment adjustment)
        {
            // Use transaction to ensure both adjustment recording and inventory update succeed or fail together
            using var transaction = await _dataService.BeginTransactionAsync();
            try
            {
                // Save the adjustment
                await _dataService.SaveAsync(adjustment);

                // Update the inventory item quantity
                var item = await _inventoryService.GetItemByIdAsync(adjustment.InventoryItemId);
                if (item != null)
                {
                    item.QuantityOnHand += adjustment.QuantityChange;
                    item.LastModifiedDate = DateTime.Now;
                    await _inventoryService.CreateOrUpdateItemAsync(item);
                }

                await transaction.CommitAsync();

                AdjustmentRecorded?.Invoke(this, EventArgs.Empty);
                return adjustment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<InventoryAdjustment>> GetAllAdjustmentsAsync()
        {
            return await _dataService.GetAllAsync<InventoryAdjustment>();
        }

        public async Task<List<InventoryAdjustment>> GetAdjustmentsByItemIdAsync(Guid itemId)
        {
            var allAdjustments = await GetAllAdjustmentsAsync();
            return allAdjustments.Where(a => a.InventoryItemId == itemId)
                                .OrderByDescending(a => a.AdjustmentDate)
                                .ToList();
        }

        public async Task<List<InventoryAdjustment>> GetAdjustmentsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            // For SQLite mode, this is translated to SQL WHERE clause for optimal performance
            // For JSON mode, it filters in memory (acceptable for small datasets)
            var adjustments = await _dataService.GetWhereAsync<InventoryAdjustment>(a =>
                a.AdjustmentDate >= startDate && a.AdjustmentDate <= endDate);

            return adjustments.OrderByDescending(a => a.AdjustmentDate).ToList();
        }

        public async Task<InventoryAdjustment?> GetAdjustmentByIdAsync(Guid id)
        {
            var adjustments = await GetAllAdjustmentsAsync();
            return adjustments.FirstOrDefault(a => a.Id == id);
        }

        public async Task<InventoryAdjustment> UpdateAdjustmentAsync(InventoryAdjustment adjustment)
        {
            await _dataService.SaveAsync(adjustment);
            AdjustmentRecorded?.Invoke(this, EventArgs.Empty);
            return adjustment;
        }

        public async Task DeleteAdjustmentAsync(Guid id)
        {
            var adjustment = await GetAdjustmentByIdAsync(id);
            if (adjustment != null)
            {
                // Use transaction to ensure both inventory reversal and adjustment deletion succeed or fail together
                using var transaction = await _dataService.BeginTransactionAsync();
                try
                {
                    // Reverse the inventory change
                    var item = await _inventoryService.GetItemByIdAsync(adjustment.InventoryItemId);
                    if (item != null)
                    {
                        item.QuantityOnHand -= adjustment.QuantityChange;
                        item.LastModifiedDate = DateTime.Now;
                        await _inventoryService.CreateOrUpdateItemAsync(item);
                    }

                    await _dataService.DeleteAsync<InventoryAdjustment>(id);

                    await transaction.CommitAsync();

                    AdjustmentRecorded?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}
