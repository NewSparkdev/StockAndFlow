using Microsoft.EntityFrameworkCore;
using StockAndFlow.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace StockAndFlow.Data
{
    public class DataMigrationService
    {
        private readonly string _dataDirectory;
        private readonly StockAndFlowDbContext _context;

        public DataMigrationService(string dataDirectory, StockAndFlowDbContext context)
        {
            _dataDirectory = dataDirectory;
            _context = context;
        }

        public async Task<MigrationResult> MigrateFromJsonAsync()
        {
            var result = new MigrationResult();

            try
            {
                // Ensure database is created
                _context.EnsureDatabaseCreated();

                // Migrate InventoryItems
                try
                {
                    var inventoryPath = Path.Combine(_dataDirectory, "inventoryitems.json");
                    if (File.Exists(inventoryPath))
                    {
                        var items = await ReadJsonFileAsync<InventoryItem>(inventoryPath);
                        if (items.Any())
                        {
                            // Clean up null values for non-nullable strings
                            foreach (var item in items)
                            {
                                item.Name = item.Name ?? string.Empty;
                                item.ImagePath = item.ImagePath ?? string.Empty;
                            }

                            await _context.InventoryItems.AddRangeAsync(items);
                            await _context.SaveChangesAsync();
                            result.InventoryItemsMigrated = items.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to migrate InventoryItems: {ex.Message}", ex);
                }

                // Migrate Sales
                try
                {
                    var salesPath = Path.Combine(_dataDirectory, "sales.json");
                    if (File.Exists(salesPath))
                    {
                        var items = await ReadJsonFileAsync<Sale>(salesPath);
                        if (items.Any())
                        {
                            // Clean up null values and ensure TransactionId is set
                            foreach (var sale in items)
                            {
                                _ = sale.TransactionId; // This will auto-generate if empty
                                sale.ItemName = sale.ItemName ?? string.Empty;
                            }

                            await _context.Sales.AddRangeAsync(items);
                            await _context.SaveChangesAsync();
                            result.SalesMigrated = items.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to migrate Sales: {ex.Message}", ex);
                }

                // Migrate Expenses
                try
                {
                    var expensesPath = Path.Combine(_dataDirectory, "expenses.json");
                    if (File.Exists(expensesPath))
                    {
                        var items = await ReadJsonFileAsync<Expense>(expensesPath);
                        if (items.Any())
                        {
                            // Clean up null values for non-nullable strings
                            foreach (var expense in items)
                            {
                                expense.Category = expense.Category ?? string.Empty;
                                expense.ReceiptImagePath = expense.ReceiptImagePath ?? string.Empty;
                            }

                            await _context.Expenses.AddRangeAsync(items);
                            await _context.SaveChangesAsync();
                            result.ExpensesMigrated = items.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to migrate Expenses: {ex.Message}", ex);
                }

                // Migrate InventoryAdjustments
                try
                {
                    var adjustmentsPath = Path.Combine(_dataDirectory, "inventoryadjustments.json");
                    if (File.Exists(adjustmentsPath))
                    {
                        var items = await ReadJsonFileAsync<InventoryAdjustment>(adjustmentsPath);
                        if (items.Any())
                        {
                            // Clean up null values for non-nullable strings
                            foreach (var adjustment in items)
                            {
                                adjustment.InventoryItemName = adjustment.InventoryItemName ?? string.Empty;
                            }

                            await _context.InventoryAdjustments.AddRangeAsync(items);
                            await _context.SaveChangesAsync();
                            result.AdjustmentsMigrated = items.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to migrate InventoryAdjustments: {ex.Message}", ex);
                }

                // Migrate BusinessSettings (only first entry if multiple exist)
                try
                {
                    var businessSettingsPath = Path.Combine(_dataDirectory, "businesssettingss.json");
                    if (File.Exists(businessSettingsPath))
                    {
                        var items = await ReadJsonFileAsync<BusinessSettings>(businessSettingsPath);
                        if (items.Any())
                        {
                            // Only take the first business settings entry
                            await _context.BusinessSettings.AddAsync(items.First());
                            await _context.SaveChangesAsync();
                            result.BusinessSettingsMigrated = 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to migrate BusinessSettings: {ex.Message}", ex);
                }

                result.Success = true;
                result.Message = $"Migration completed successfully. " +
                                $"Migrated {result.TotalRecordsMigrated} records.";

                // Back up JSON files
                await BackupJsonFilesAsync();
            }
            catch (Exception ex)
            {
                result.Success = false;
                var innerMessage = ex.InnerException?.Message ?? "No inner exception";
                var innerStackTrace = ex.InnerException?.StackTrace ?? "No stack trace";
                result.Message = $"Migration failed: {ex.Message}\n\nInner Exception: {innerMessage}\n\nStack Trace: {innerStackTrace}";

                // Log to file for debugging
                var logPath = Path.Combine(_dataDirectory, "migration_error.log");
                File.WriteAllText(logPath, $"Exception: {ex}\n\nInner Exception: {ex.InnerException}\n\nStack Trace: {ex.StackTrace}");
            }

            return result;
        }

        public async Task<bool> HasJsonDataAsync()
        {
            return await Task.Run(() =>
            {
                var jsonFiles = new[]
                {
                    "inventoryitems.json",
                    "sales.json",
                    "expenses.json",
                    "inventoryadjustments.json",
                    "businesssettingss.json"
                };

                return jsonFiles.Any(file =>
                {
                    var path = Path.Combine(_dataDirectory, file);
                    return File.Exists(path) && new FileInfo(path).Length > 2; // More than just "[]"
                });
            });
        }

        public async Task<bool> HasDatabaseDataAsync()
        {
            var inventoryCount = await _context.InventoryItems.CountAsync();
            return inventoryCount > 0;
        }

        private async Task<List<T>> ReadJsonFileAsync<T>(string filePath)
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                    return new List<T>();

                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                    return new List<T>();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                return JsonSerializer.Deserialize<List<T>>(json, options) ?? new List<T>();
            });
        }

        private async Task BackupJsonFilesAsync()
        {
            await Task.Run(() =>
            {
                var backupDir = Path.Combine(_dataDirectory, "JsonBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);

                var jsonFiles = new[]
                {
                    "inventoryitems.json",
                    "sales.json",
                    "expenses.json",
                    "inventoryadjustments.json",
                    "businesssettingss.json",
                    "settings.json",
                    "import_history.json"
                };

                foreach (var file in jsonFiles)
                {
                    var sourcePath = Path.Combine(_dataDirectory, file);
                    if (File.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(backupDir, file);
                        File.Copy(sourcePath, destPath, true);
                    }
                }
            });
        }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int InventoryItemsMigrated { get; set; }
        public int SalesMigrated { get; set; }
        public int ExpensesMigrated { get; set; }
        public int AdjustmentsMigrated { get; set; }
        public int BusinessSettingsMigrated { get; set; }

        public int TotalRecordsMigrated =>
            InventoryItemsMigrated +
            SalesMigrated +
            ExpensesMigrated +
            AdjustmentsMigrated +
            BusinessSettingsMigrated;
    }
}
