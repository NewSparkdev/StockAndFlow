using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Serilog;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class ExportImportService
    {
        private readonly IDataService _dataService;
        private readonly InventoryService _inventoryService;
        private readonly SalesService _salesService;
        private readonly ExpenseService _expenseService;
        private readonly string _importHistoryFile;

        public ExportImportService(
            IDataService dataService,
            InventoryService inventoryService,
            SalesService salesService,
            ExpenseService expenseService,
            Platform.IPathProvider? pathProvider = null)
        {
            _dataService = dataService;
            _inventoryService = inventoryService;
            _salesService = salesService;
            _expenseService = expenseService;
            // Must be an absolute, writable location: a relative "Data" path resolves against the
            // process working directory, which on Android/iOS is outside the app sandbox — writing
            // there threw *after* the import had committed, masking the real result with a crash.
            _importHistoryFile = Path.Combine(
                pathProvider?.DataDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "Data"),
                "import_history.json");
        }

        /// <summary>
        /// Free-tier import allowance. Settable rather than injected to avoid a circular
        /// dependency (EntitlementService needs InventoryService, which this also uses); the host
        /// assigns it once at startup. Null means unrestricted, so tests and the desktop app
        /// behave as before.
        /// </summary>
        public EntitlementService? Entitlements { get; set; }

        public async Task<string> ExportToExcelAsync(string filePath)
        {
            // Validate file path to prevent path traversal attacks
            ValidateFilePath(filePath, ".xlsx");

            var workbook = new XLWorkbook();

            // Get all data
            var inventory = await _inventoryService.GetAllItemsAsync();
            var sales = await _salesService.GetAllSalesAsync();
            var expenses = await _expenseService.GetAllExpensesAsync();

            // Create Inventory sheet
            var invSheet = workbook.Worksheets.Add("Inventory");
            CreateInventorySheet(invSheet, inventory);

            // Create Sales sheet
            var salesSheet = workbook.Worksheets.Add("Sales");
            CreateSalesSheet(salesSheet, sales);

            // Create Expenses sheet
            var expSheet = workbook.Worksheets.Add("Expenses");
            CreateExpensesSheet(expSheet, expenses);

            // Create Instructions sheet
            var instrSheet = workbook.Worksheets.Add("Instructions");
            CreateInstructionsSheet(instrSheet);

            // Save file
            workbook.SaveAs(filePath);

            return filePath;
        }

        private void CreateInventorySheet(IXLWorksheet sheet, List<InventoryItem> items)
        {
            // Headers
            sheet.Cell(1, 1).Value = "ID";
            sheet.Cell(1, 2).Value = "Name";
            sheet.Cell(1, 3).Value = "SKU";
            sheet.Cell(1, 4).Value = "Category";
            sheet.Cell(1, 5).Value = "Cost Per Unit";
            sheet.Cell(1, 6).Value = "Sale Price";
            sheet.Cell(1, 7).Value = "Quantity";
            sheet.Cell(1, 8).Value = "Profit Per Unit";
            sheet.Cell(1, 9).Value = "Total Value";
            // Appended (not inserted) so spreadsheets exported by older versions still import.
            // Omitting these made "backup to Excel" silently lose how an item is measured,
            // its restock level, labor cost, supplier and notes.
            sheet.Cell(1, 10).Value = "Unit";
            sheet.Cell(1, 11).Value = "Min Stock Level";
            sheet.Cell(1, 12).Value = "Extra Cost Per Unit";
            sheet.Cell(1, 13).Value = "Supplier";
            sheet.Cell(1, 14).Value = "Notes";
            sheet.Cell(1, 15).Value = "Sold To Customers";

            // Style headers
            var headerRange = sheet.Range(1, 1, 1, 15);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data
            int row = 2;
            foreach (var item in items)
            {
                sheet.Cell(row, 1).Value = item.Id.ToString();
                sheet.Cell(row, 2).Value = item.Name;
                sheet.Cell(row, 3).Value = item.Sku ?? "";
                sheet.Cell(row, 4).Value = item.Category ?? "";
                sheet.Cell(row, 5).Value = item.CostPerUnit;
                sheet.Cell(row, 6).Value = item.SalePrice;
                sheet.Cell(row, 7).Value = item.QuantityOnHand;

                // Formula for Profit Per Unit
                sheet.Cell(row, 8).FormulaA1 = $"=F{row}-E{row}";

                // Formula for Total Value
                sheet.Cell(row, 9).FormulaA1 = $"=E{row}*G{row}";

                sheet.Cell(row, 10).Value = item.UnitOfMeasure;
                sheet.Cell(row, 11).Value = item.MinimumStockLevel;
                sheet.Cell(row, 12).Value = item.ExtraCostPerUnit;
                sheet.Cell(row, 13).Value = item.Supplier ?? "";
                sheet.Cell(row, 14).Value = item.Notes ?? "";
                sheet.Cell(row, 15).Value = item.IsSellable ? "Yes" : "No";

                row++;
            }

            // Format currency columns
            sheet.Column(5).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(6).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(8).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(9).Style.NumberFormat.Format = "$#,##0.00";

            // Auto-fit columns
            sheet.Columns().AdjustToContents();
        }

        private void CreateSalesSheet(IXLWorksheet sheet, List<Sale> sales)
        {
            // Headers
            sheet.Cell(1, 1).Value = "ID";
            sheet.Cell(1, 2).Value = "Transaction ID";
            sheet.Cell(1, 3).Value = "Sale Date";
            sheet.Cell(1, 4).Value = "Item Name";
            sheet.Cell(1, 5).Value = "Inventory Item ID";
            sheet.Cell(1, 6).Value = "Quantity";
            sheet.Cell(1, 7).Value = "Sale Price Per Unit";
            sheet.Cell(1, 8).Value = "Cost Per Unit";
            sheet.Cell(1, 9).Value = "Revenue";
            sheet.Cell(1, 10).Value = "COGS";
            sheet.Cell(1, 11).Value = "Profit";
            sheet.Cell(1, 12).Value = "Customer Name";
            sheet.Cell(1, 13).Value = "Notes";
            sheet.Cell(1, 14).Value = "Unit";

            // Style headers
            var headerRange = sheet.Range(1, 1, 1, 14);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data
            int row = 2;
            foreach (var sale in sales.OrderByDescending(s => s.SaleDate))
            {
                sheet.Cell(row, 1).Value = sale.Id.ToString();
                sheet.Cell(row, 2).Value = sale.TransactionId.ToString();
                sheet.Cell(row, 3).Value = sale.SaleDate;
                sheet.Cell(row, 4).Value = sale.ItemName;
                sheet.Cell(row, 5).Value = sale.InventoryItemId.ToString();
                sheet.Cell(row, 6).Value = sale.Quantity;
                sheet.Cell(row, 7).Value = sale.SalePricePerUnit;
                sheet.Cell(row, 8).Value = sale.CostPerUnit;

                // Formulas
                sheet.Cell(row, 9).FormulaA1 = $"=F{row}*G{row}";  // Revenue
                sheet.Cell(row, 10).FormulaA1 = $"=F{row}*H{row}";  // COGS
                sheet.Cell(row, 11).FormulaA1 = $"=I{row}-J{row}"; // Profit

                sheet.Cell(row, 12).Value = sale.CustomerName ?? "";
                sheet.Cell(row, 13).Value = sale.Notes ?? "";
                sheet.Cell(row, 14).Value = sale.UnitOfMeasure;

                row++;
            }

            // Format columns
            sheet.Column(3).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Column(7).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(8).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(9).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(10).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Column(11).Style.NumberFormat.Format = "$#,##0.00";

            sheet.Columns().AdjustToContents();
        }

        private void CreateExpensesSheet(IXLWorksheet sheet, List<Expense> expenses)
        {
            // Headers
            sheet.Cell(1, 1).Value = "ID";
            sheet.Cell(1, 2).Value = "Expense Date";
            sheet.Cell(1, 3).Value = "Category";
            sheet.Cell(1, 4).Value = "Amount";
            sheet.Cell(1, 5).Value = "Description";
            sheet.Cell(1, 6).Value = "Linked Item ID";

            // Style headers
            var headerRange = sheet.Range(1, 1, 1, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightCoral;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data
            int row = 2;
            foreach (var expense in expenses.OrderByDescending(e => e.ExpenseDate))
            {
                sheet.Cell(row, 1).Value = expense.Id.ToString();
                sheet.Cell(row, 2).Value = expense.ExpenseDate;
                sheet.Cell(row, 3).Value = expense.Category;
                sheet.Cell(row, 4).Value = expense.Amount;
                sheet.Cell(row, 5).Value = expense.Description ?? "";
                sheet.Cell(row, 6).Value = expense.InventoryItemId?.ToString() ?? "";

                row++;
            }

            // Format columns
            sheet.Column(2).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Column(4).Style.NumberFormat.Format = "$#,##0.00";

            sheet.Columns().AdjustToContents();
        }

        private void CreateInstructionsSheet(IXLWorksheet sheet)
        {
            sheet.Cell(1, 1).Value = "Stock & Flow - Excel Export/Import Instructions";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 16;

            sheet.Cell(3, 1).Value = "IMPORTANT NOTES:";
            sheet.Cell(3, 1).Style.Font.Bold = true;
            sheet.Cell(3, 1).Style.Font.FontColor = XLColor.Red;

            sheet.Cell(4, 1).Value = "• Do NOT delete or modify the ID column - it's used to match existing records";
            sheet.Cell(5, 1).Value = "• To ADD new records: Add a new row and leave the ID column empty (a new ID will be generated)";
            sheet.Cell(6, 1).Value = "• To UPDATE existing records: Modify data in existing rows (keep the ID)";
            sheet.Cell(7, 1).Value = "• Do NOT delete column headers";
            sheet.Cell(8, 1).Value = "• For Sales: Make sure the Inventory Item ID exists in the Inventory sheet";
            sheet.Cell(9, 1).Value = "• Formulas will auto-calculate (Revenue, Profit, Total Value, etc.)";

            sheet.Cell(11, 1).Value = "ADDING NEW INVENTORY:";
            sheet.Cell(11, 1).Style.Font.Bold = true;
            sheet.Cell(12, 1).Value = "1. Go to Inventory sheet";
            sheet.Cell(13, 1).Value = "2. Add a new row at the bottom";
            sheet.Cell(14, 1).Value = "3. Leave ID column empty";
            sheet.Cell(15, 1).Value = "4. Fill in: Name, SKU, Category, Cost, Price, Quantity";
            sheet.Cell(16, 1).Value = "5. Profit and Total Value will calculate automatically";

            sheet.Cell(18, 1).Value = "RECORDING A NEW SALE:";
            sheet.Cell(18, 1).Style.Font.Bold = true;
            sheet.Cell(19, 1).Value = "1. Go to Sales sheet";
            sheet.Cell(20, 1).Value = "2. Add a new row at the bottom";
            sheet.Cell(21, 1).Value = "3. Leave ID and Transaction ID columns empty (will be generated)";
            sheet.Cell(22, 1).Value = "4. To record multi-item sale: Use the same Transaction ID for multiple rows";
            sheet.Cell(23, 1).Value = "5. Enter Sale Date (YYYY-MM-DD HH:MM format)";
            sheet.Cell(24, 1).Value = "6. Copy the Inventory Item ID from the Inventory sheet";
            sheet.Cell(25, 1).Value = "7. Enter Item Name, Quantity, Sale Price, Cost Price";
            sheet.Cell(26, 1).Value = "8. Revenue, COGS, and Profit will calculate automatically";

            sheet.Cell(28, 1).Value = "ADDING AN EXPENSE:";
            sheet.Cell(28, 1).Style.Font.Bold = true;
            sheet.Cell(29, 1).Value = "1. Go to Expenses sheet";
            sheet.Cell(30, 1).Value = "2. Add a new row at the bottom";
            sheet.Cell(31, 1).Value = "3. Leave ID column empty";
            sheet.Cell(32, 1).Value = "4. Enter Date, Category, Amount, Description";

            sheet.Cell(34, 1).Value = "IMPORTING BACK:";
            sheet.Cell(34, 1).Style.Font.Bold = true;
            sheet.Cell(35, 1).Value = "1. Save this file after making changes";
            sheet.Cell(36, 1).Value = "2. In Stock & Flow, go to Reports tab";
            sheet.Cell(37, 1).Value = "3. Click Export/Import button → Import";
            sheet.Cell(38, 1).Value = "4. Select this file";
            sheet.Cell(39, 1).Value = "5. The program will validate and import your changes";
            sheet.Cell(40, 1).Value = "6. You'll see a confirmation of what was added/updated";

            sheet.Column(1).Width = 80;
        }

        public async Task<ImportResult> ImportFromExcelAsync(string filePath)
        {
            var result = new ImportResult();

            // Validate file path to prevent path traversal attacks
            ValidateFilePath(filePath, ".xlsx");

            // Check if file exists
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = "File not found.";
                return result;
            }

            // Free-tier import allowance. Checked before any work, and only counted after the
            // import actually commits — a failed import must not burn an allowance.
            if (Entitlements != null)
            {
                var check = await Entitlements.CanImportAsync();
                if (!check.Allowed)
                {
                    result.Success = false;
                    result.ErrorMessage = check.Message ?? "Import limit reached.";
                    result.BlockedByEntitlement = check;
                    return result;
                }
            }

            // Check if this file has been imported before
            if (await HasBeenImportedAsync(filePath))
            {
                result.HasDuplicateWarning = true;
                result.DuplicateMessage = "WARNING: This file appears to have been imported before. Importing again may create duplicate records.";
            }

            // Use transaction to ensure all imports succeed or fail together
            var committed = false;
            using var transaction = await _dataService.BeginTransactionAsync();
            try
            {
                // Read into memory and open from the stream rather than handing ClosedXML the
                // path: when the file isn't a real spreadsheet, opening by path leaves a handle
                // behind and the user can't delete or overwrite their own file afterwards.
                using var fileBytes = new MemoryStream(await File.ReadAllBytesAsync(filePath));
                using var workbook = new XLWorkbook(fileBytes);

                // Import Inventory
                if (workbook.Worksheets.Contains("Inventory"))
                {
                    var invResult = await ImportInventorySheet(workbook.Worksheet("Inventory"));
                    result.InventoryAdded = invResult.Added;
                    result.InventoryUpdated = invResult.Updated;
                }

                // Import Sales
                if (workbook.Worksheets.Contains("Sales"))
                {
                    var salesResult = await ImportSalesSheet(workbook.Worksheet("Sales"));
                    result.SalesAdded = salesResult.Added;
                    result.SalesUpdated = salesResult.Updated;
                }

                // Import Expenses
                if (workbook.Worksheets.Contains("Expenses"))
                {
                    var expResult = await ImportExpensesSheet(workbook.Worksheet("Expenses"));
                    result.ExpensesAdded = expResult.Added;
                    result.ExpensesUpdated = expResult.Updated;
                }

                // Commit transaction before recording import history
                await transaction.CommitAsync();
                committed = true;

                // Bookkeeping for the "already imported" warning only — never let it fail the
                // import that already succeeded.
                try
                {
                    await RecordImportAsync(filePath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not record import history at {Path}", _importHistoryFile);
                }

                if (Entitlements != null)
                    await Entitlements.RecordImportAsync();

                result.Success = true;
            }
            catch (Exception ex)
            {
                // Only roll back work that is still open; rolling back a committed transaction
                // throws and masks the original error.
                if (!committed)
                {
                    try { await transaction.RollbackAsync(); }
                    catch (Exception rollbackEx) { Log.Error(rollbackEx, "Import rollback failed"); }
                }

                Log.Error(ex, "Excel import failed for {File}", filePath);
                result.Success = false;
                result.ErrorMessage = $"Import failed: {ex.Message}";
            }

            return result;
        }

        private async Task<(int Added, int Updated)> ImportInventorySheet(IXLWorksheet sheet)
        {
            int added = 0, updated = 0;
            var existingItems = await _inventoryService.GetAllItemsAsync();
            var headers = BuildHeaderMap(sheet);

            var rows = sheet.RowsUsed().Skip(1); // Skip header

            foreach (var row in rows)
            {
                var idStr = row.Cell(1).GetString();
                var name = row.Cell(2).GetString();

                if (string.IsNullOrWhiteSpace(name)) continue;

                InventoryItem item;
                bool isNew = string.IsNullOrWhiteSpace(idStr);

                if (isNew)
                {
                    item = new InventoryItem();
                    added++;
                }
                else
                {
                    var id = Guid.Parse(idStr);
                    item = existingItems.FirstOrDefault(i => i.Id == id);
                    if (item == null)
                    {
                        item = new InventoryItem { Id = id };
                        added++;
                    }
                    else
                    {
                        updated++;
                    }
                }

                item.Name = name;
                item.Sku = row.Cell(3).GetString();
                item.Category = row.Cell(4).GetString();
                item.CostPerUnit = (decimal)row.Cell(5).GetDouble();
                item.SalePrice = (decimal)row.Cell(6).GetDouble();
                item.QuantityOnHand = (decimal)row.Cell(7).GetDouble();

                // Columns added later: absent in spreadsheets from older versions, so only
                // apply them when the column exists — never blank out a value the file
                // simply doesn't carry.
                var unit = ReadString(row, headers, "Unit");
                if (!string.IsNullOrWhiteSpace(unit))
                    item.UnitOfMeasure = unit;

                var minStock = ReadDecimal(row, headers, "Min Stock Level");
                if (minStock.HasValue)
                    item.MinimumStockLevel = minStock.Value;

                var extraCost = ReadDecimal(row, headers, "Extra Cost Per Unit");
                if (extraCost.HasValue)
                    item.ExtraCostPerUnit = extraCost.Value;

                if (headers.ContainsKey("Supplier"))
                    item.Supplier = ReadString(row, headers, "Supplier");
                if (headers.ContainsKey("Notes"))
                    item.Notes = ReadString(row, headers, "Notes");

                var sold = ReadString(row, headers, "Sold To Customers");
                if (!string.IsNullOrWhiteSpace(sold))
                {
                    // Spreadsheets get hand-edited, so accept the obvious spellings.
                    item.IsSellable = !(sold.Trim().StartsWith("N", StringComparison.OrdinalIgnoreCase)
                                        || sold.Trim() == "0"
                                        || sold.Trim().Equals("false", StringComparison.OrdinalIgnoreCase));
                }

                item.LastModifiedDate = DateTime.Now;

                await _inventoryService.CreateOrUpdateItemAsync(item);
            }

            WarnAboutDuplicateSkus(await _inventoryService.GetAllItemsAsync());

            return (added, updated);
        }

        private async Task<(int Added, int Updated)> ImportSalesSheet(IXLWorksheet sheet)
        {
            int added = 0, updated = 0;
            var existingSales = await _salesService.GetAllSalesAsync();
            var headers = BuildHeaderMap(sheet);

            var rows = sheet.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                var idStr = row.Cell(1).GetString();
                var transactionIdStr = row.Cell(2).GetString();
                var itemName = row.Cell(4).GetString();

                if (string.IsNullOrWhiteSpace(itemName)) continue;

                var sale = new Sale
                {
                    SaleDate = row.Cell(3).GetDateTime(),
                    ItemName = itemName,
                    InventoryItemId = Guid.Parse(row.Cell(5).GetString()),
                    Quantity = (decimal)row.Cell(6).GetDouble(),
                    SalePricePerUnit = (decimal)row.Cell(7).GetDouble(),
                    CostPerUnit = (decimal)row.Cell(8).GetDouble(),
                    CustomerName = row.Cell(12).GetString(),
                    Notes = row.Cell(13).GetString()
                };

                // Column added later; spreadsheets from older versions predate measured sales.
                var saleUnit = ReadString(row, headers, "Unit");
                if (!string.IsNullOrWhiteSpace(saleUnit))
                    sale.UnitOfMeasure = saleUnit;

                // Set TransactionId if it exists, otherwise generate new
                if (!string.IsNullOrWhiteSpace(transactionIdStr) && Guid.TryParse(transactionIdStr, out var transactionId))
                {
                    sale.TransactionId = transactionId;
                }

                // If ID exists, use it (for restore); otherwise generate new
                if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var saleId))
                {
                    sale.Id = saleId;
                    // Check if this ID already exists
                    if (existingSales.Any(s => s.Id == saleId))
                    {
                        updated++;
                    }
                    else
                    {
                        added++;
                    }
                }
                else
                {
                    added++;
                }

                await _dataService.SaveAsync(sale);
            }

            return (added, updated);
        }

        private async Task<(int Added, int Updated)> ImportExpensesSheet(IXLWorksheet sheet)
        {
            int added = 0, updated = 0;
            var existingExpenses = await _expenseService.GetAllExpensesAsync();

            var rows = sheet.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                var idStr = row.Cell(1).GetString();
                var category = row.Cell(3).GetString();

                if (string.IsNullOrWhiteSpace(category)) continue;

                var expense = new Expense
                {
                    ExpenseDate = row.Cell(2).GetDateTime(),
                    Category = category,
                    Amount = (decimal)row.Cell(4).GetDouble(),
                    Description = row.Cell(5).GetString()
                };

                var linkedIdStr = row.Cell(6).GetString();
                if (!string.IsNullOrWhiteSpace(linkedIdStr))
                {
                    expense.InventoryItemId = Guid.Parse(linkedIdStr);
                }

                // If ID exists, use it (for restore); otherwise generate new
                if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var expenseId))
                {
                    expense.Id = expenseId;
                    // Check if this ID already exists
                    if (existingExpenses.Any(e => e.Id == expenseId))
                    {
                        updated++;
                    }
                    else
                    {
                        added++;
                    }
                }
                else
                {
                    added++;
                }

                await _expenseService.CreateOrUpdateExpenseAsync(expense);
            }

            return (added, updated);
        }

        private async Task<bool> HasBeenImportedAsync(string filePath)
        {
            // Advisory only: if the history is unreadable, proceed without the warning rather
            // than blocking an otherwise valid import.
            try
            {
                if (!File.Exists(_importHistoryFile))
                    return false;

                var json = await File.ReadAllTextAsync(_importHistoryFile);
                var history = System.Text.Json.JsonSerializer.Deserialize<List<ImportHistoryEntry>>(json) ?? new List<ImportHistoryEntry>();

                var fileInfo = new FileInfo(filePath);
                return history.Any(h => h.FileName == fileInfo.Name && h.FileSize == fileInfo.Length);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not read import history at {Path}", _importHistoryFile);
                return false;
            }
        }

        private async Task RecordImportAsync(string filePath)
        {
            List<ImportHistoryEntry> history = new();

            if (File.Exists(_importHistoryFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_importHistoryFile);
                    history = System.Text.Json.JsonSerializer.Deserialize<List<ImportHistoryEntry>>(json) ?? new List<ImportHistoryEntry>();
                }
                catch (System.Text.Json.JsonException)
                {
                    // Corrupt history file — start a fresh one rather than failing the import.
                    history = new List<ImportHistoryEntry>();
                }
            }
            else
            {
                var dir = Path.GetDirectoryName(_importHistoryFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }

            var fileInfo = new FileInfo(filePath);
            history.Add(new ImportHistoryEntry
            {
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                ImportedAt = DateTime.Now
            });

            var newJson = System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_importHistoryFile, newJson);
        }

        /// <summary>
        /// A spreadsheet can introduce codes that clash with existing items. The import isn't
        /// blocked (the user may be mid-cleanup), but a shared code means scanning it picks
        /// whichever item matches first, so it must not pass unremarked.
        /// </summary>
        private static void WarnAboutDuplicateSkus(List<InventoryItem> items)
        {
            var clashes = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Sku))
                .GroupBy(i => i.Sku!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var clash in clashes)
            {
                Log.Warning("Duplicate barcode {Sku} shared by {Count} items ({Names}) — scanning it is ambiguous",
                    clash.Key, clash.Count(), string.Join(", ", clash.Select(i => i.Name)));
            }
        }

        /// <summary>
        /// Maps header text to column index so imports can tolerate spreadsheets written by
        /// older versions (fewer columns) without reading values out of the wrong cells.
        /// </summary>
        private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet sheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = sheet.Row(1);
            foreach (var cell in headerRow.CellsUsed())
            {
                var name = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(name))
                    map[name] = cell.Address.ColumnNumber;
            }
            return map;
        }

        private static string? ReadString(IXLRow row, Dictionary<string, int> headers, string column)
            => headers.TryGetValue(column, out var index) ? row.Cell(index).GetString() : null;

        private static decimal? ReadDecimal(IXLRow row, Dictionary<string, int> headers, string column)
        {
            if (!headers.TryGetValue(column, out var index))
                return null;

            var cell = row.Cell(index);
            if (cell.IsEmpty())
                return null;
            if (cell.TryGetValue<double>(out var number))
                return (decimal)number;
            return decimal.TryParse(cell.GetString(), out var parsed) ? parsed : null;
        }

        private void ValidateFilePath(string filePath, string expectedExtension)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            }

            // Get the full path to prevent relative path exploits
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid file path: {ex.Message}", nameof(filePath), ex);
            }

            // Validate extension
            var extension = Path.GetExtension(fullPath);
            if (!extension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"File must have {expectedExtension} extension.", nameof(filePath));
            }

            // Check for path traversal attempts
            var fileName = Path.GetFileName(fullPath);
            if (fileName.Contains("..") || fullPath.Contains(".."))
            {
                throw new ArgumentException("Path traversal is not allowed.", nameof(filePath));
            }

            // Ensure path doesn't point to system directories
            var systemDirectories = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var sysDir in systemDirectories)
            {
                if (!string.IsNullOrEmpty(sysDir) && fullPath.StartsWith(sysDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Cannot access system directories.", nameof(filePath));
                }
            }
        }
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Set when the import was refused by the free-tier allowance rather than failing.
        /// Lets the UI offer an upgrade instead of showing an error.
        /// </summary>
        public EntitlementResult? BlockedByEntitlement { get; set; }

        public bool HasDuplicateWarning { get; set; }
        public string? DuplicateMessage { get; set; }
        public int InventoryAdded { get; set; }
        public int InventoryUpdated { get; set; }
        public int SalesAdded { get; set; }
        public int SalesUpdated { get; set; }
        public int ExpensesAdded { get; set; }
        public int ExpensesUpdated { get; set; }

        public string GetSummary()
        {
            var summary = "Import completed successfully!\n\n";
            summary += $"Inventory: {InventoryAdded} added, {InventoryUpdated} updated\n";
            summary += $"Sales: {SalesAdded} added\n";
            summary += $"Expenses: {ExpensesAdded} added\n";
            return summary;
        }
    }

    public class ImportHistoryEntry
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime ImportedAt { get; set; }
    }
}
