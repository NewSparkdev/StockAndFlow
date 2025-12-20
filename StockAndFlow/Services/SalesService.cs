using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var item = await _inventoryService.GetItemByIdAsync(inventoryItemId);
            if (item == null)
                return null;

            var settings = await _dataService.GetSettingsAsync();
            if (item.QuantityOnHand < quantity && !settings.AllowNegativeInventory)
                return null; // Insufficient inventory

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

            await _dataService.SaveAsync(sale);

            // Adjust inventory
            await _inventoryService.AdjustQuantityAsync(inventoryItemId, -quantity);

            SaleRecorded?.Invoke(this, EventArgs.Empty);
            return sale;
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
            var sales = await GetAllSalesAsync();
            return sales.Where(s => s.SaleDate >= start && s.SaleDate <= end)
                       .OrderByDescending(s => s.SaleDate)
                       .ToList();
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
                return;

            if (restoreInventory)
            {
                await _inventoryService.AdjustQuantityAsync(
                    sale.InventoryItemId,
                    sale.Quantity);
            }

            await _dataService.DeleteAsync<Sale>(saleId);
            SaleRecorded?.Invoke(this, EventArgs.Empty);
        }
    }
}
