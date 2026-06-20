using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class SalesService
    {
        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;

        public event EventHandler? SaleRecorded;

        public SalesService(IDataService dataService, InventoryService inventoryService)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
        }

        public async Task<List<Sale>> GetAllSalesAsync()
        {
            return await _dataService.GetAllAsync<Sale>();
        }

        public async Task<Sale?> GetSaleByIdAsync(Guid id)
        {
            return await _dataService.GetByIdAsync<Sale>(id);
        }

        public async Task<Sale?> RecordSaleAsync(
            Guid inventoryItemId,
            int quantity,
            decimal? customSalePrice = null,
            string? customerName = null,
            string? customerEmail = null,
            string? notes = null,
            Guid? transactionId = null,
            DateTime? saleDate = null,
            string? taxStateCode = null,
            decimal taxRate = 0,
            decimal taxAmount = 0)
        {
            // Validate quantity
            if (quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
            }

            // Validate custom sale price if provided
            if (customSalePrice.HasValue && customSalePrice.Value < 0)
            {
                throw new ArgumentException("Sale price cannot be negative", nameof(customSalePrice));
            }

            // Validate tax values
            if (taxRate < 0)
            {
                throw new ArgumentException("Tax rate cannot be negative", nameof(taxRate));
            }

            if (taxAmount < 0)
            {
                throw new ArgumentException("Tax amount cannot be negative", nameof(taxAmount));
            }

            var item = await _inventoryService.GetItemByIdAsync(inventoryItemId);
            if (item == null)
            {
                throw new InvalidOperationException($"Inventory item with ID {inventoryItemId} not found");
            }

            var settings = await _dataService.GetSettingsAsync();
            if (item.QuantityOnHand < quantity && !settings.AllowNegativeInventory)
            {
                throw new InvalidOperationException(
                    $"Insufficient inventory for '{item.Name}'. Available: {item.QuantityOnHand}, Requested: {quantity}. " +
                    "Enable 'Allow Negative Inventory' in settings to allow overselling.");
            }

            var sale = new Sale
            {
                TransactionId = transactionId ?? Guid.NewGuid(),
                InventoryItemId = inventoryItemId,
                ItemName = item.Name,
                Quantity = quantity,
                SalePricePerUnit = customSalePrice ?? item.SalePrice,
                CostPerUnit = item.CostPerUnit,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                Notes = notes,
                SaleDate = saleDate ?? DateTime.Now,
                TaxStateCode = taxStateCode,
                TaxRate = taxRate,
                TaxAmount = taxAmount
            };

            // Use transaction to ensure both sale recording and inventory adjustment succeed or fail together
            using var transaction = await _dataService.BeginTransactionAsync();
            try
            {
                await _dataService.SaveAsync(sale);

                // Adjust inventory
                await _inventoryService.AdjustQuantityAsync(inventoryItemId, -quantity);

                await transaction.CommitAsync();

                Log.Information("Sale recorded successfully: {ItemName} x{Quantity} for {Revenue:C}",
                    sale.ItemName, sale.Quantity, sale.Revenue);

                SaleRecorded?.Invoke(this, EventArgs.Empty);
                return sale;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, "Failed to record sale for item {ItemId}, quantity {Quantity}",
                    inventoryItemId, quantity);
                throw new InvalidOperationException($"Failed to record sale: {ex.Message}", ex);
            }
        }

        public async Task<List<Sale>> GetSalesByItemAsync(Guid inventoryItemId)
        {
            var sales = await GetAllSalesAsync();
            return sales.Where(s => s.InventoryItemId == inventoryItemId)
                       .OrderByDescending(s => s.SaleDate)
                       .ToList();
        }

        public async Task<List<Sale>> GetSalesByDateRangeAsync(DateTime start, DateTime end)
        {
            // For SQLite mode, this is translated to SQL WHERE clause for optimal performance
            // For JSON mode, it filters in memory (acceptable for small datasets)
            var sales = await _dataService.GetWhereAsync<Sale>(s =>
                s.SaleDate >= start && s.SaleDate <= end);

            return sales.OrderByDescending(s => s.SaleDate).ToList();
        }

        public async Task<Sale> UpdateSaleAsync(Sale sale)
        {
            await _dataService.SaveAsync(sale);
            SaleRecorded?.Invoke(this, EventArgs.Empty);
            return sale;
        }

        public async Task DeleteSaleAsync(Guid saleId, bool restoreInventory = true)
        {
            var sale = await GetSaleByIdAsync(saleId);
            if (sale == null)
            {
                throw new InvalidOperationException($"Sale with ID {saleId} not found");
            }

            // Use transaction to ensure both inventory restoration and sale deletion succeed or fail together
            using var transaction = await _dataService.BeginTransactionAsync();
            try
            {
                if (restoreInventory)
                {
                    await _inventoryService.AdjustQuantityAsync(
                        sale.InventoryItemId,
                        sale.Quantity);
                }

                await _dataService.DeleteAsync<Sale>(saleId);

                await transaction.CommitAsync();

                Log.Information("Sale deleted: {SaleId}, Item: {ItemName}, Quantity: {Quantity}, Inventory restored: {Restored}",
                    saleId, sale.ItemName, sale.Quantity, restoreInventory);

                SaleRecorded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, "Failed to delete sale {SaleId}", saleId);
                throw new InvalidOperationException($"Failed to delete sale: {ex.Message}", ex);
            }
        }
    }
}
