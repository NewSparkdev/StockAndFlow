using Microsoft.Data.Sqlite;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StockAndFlow.Data
{
    /// <summary>
    /// Helper class to handle manual database migrations for SQLite.
    /// Used when EnsureCreated() is used instead of EF Core migrations.
    /// </summary>
    public static class DatabaseMigrationHelper
    {
        /// <summary>
        /// Applies schema updates to an existing database.
        /// Safe to run multiple times - checks if columns exist before adding them.
        /// </summary>
        public static async Task ApplySchemaUpdatesAsync(string databasePath)
        {
            if (!File.Exists(databasePath))
            {
                Log.Information("Database doesn't exist yet, no migration needed");
                return;
            }

            var connectionString = $"Data Source={databasePath}";

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            try
            {
                Log.Information("Checking database schema for updates...");

                // Add IsDeleted and DeletedDate columns to InventoryItems if they don't exist
                await AddColumnIfNotExistsAsync(connection, "InventoryItems", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
                await AddColumnIfNotExistsAsync(connection, "InventoryItems", "DeletedDate", "TEXT NULL");

                // Unit of measure for the measure-by-weight/volume feature ("each" = counted)
                await AddColumnIfNotExistsAsync(connection, "InventoryItems", "UnitOfMeasure", "TEXT NOT NULL DEFAULT 'each'");

                // Create indexes if they don't exist
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryItems_Name", "InventoryItems", "Name");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryItems_Sku", "InventoryItems", "Sku");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryItems_Category", "InventoryItems", "Category");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryItems_ShopifyProductId", "InventoryItems", "ShopifyProductId");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryItems_IsDeleted", "InventoryItems", "IsDeleted");

                await CreateIndexIfNotExistsAsync(connection, "IX_Sales_SaleDate", "Sales", "SaleDate");
                await CreateIndexIfNotExistsAsync(connection, "IX_Sales_InventoryItemId", "Sales", "InventoryItemId");
                await CreateIndexIfNotExistsAsync(connection, "IX_Sales_ShopifyOrderId", "Sales", "ShopifyOrderId");
                await CreateIndexIfNotExistsAsync(connection, "IX_Sales_InventoryItemId_SaleDate", "Sales", "InventoryItemId, SaleDate");

                await CreateIndexIfNotExistsAsync(connection, "IX_Expenses_ExpenseDate", "Expenses", "ExpenseDate");
                await CreateIndexIfNotExistsAsync(connection, "IX_Expenses_Category", "Expenses", "Category");
                await CreateIndexIfNotExistsAsync(connection, "IX_Expenses_InventoryItemId", "Expenses", "InventoryItemId");
                await CreateIndexIfNotExistsAsync(connection, "IX_Expenses_Category_ExpenseDate", "Expenses", "Category, ExpenseDate");

                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryAdjustments_AdjustmentDate", "InventoryAdjustments", "AdjustmentDate");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryAdjustments_InventoryItemId", "InventoryAdjustments", "InventoryItemId");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryAdjustments_Reason", "InventoryAdjustments", "Reason");
                await CreateIndexIfNotExistsAsync(connection, "IX_InventoryAdjustments_InventoryItemId_AdjustmentDate", "InventoryAdjustments", "InventoryItemId, AdjustmentDate");

                // Create BomComponents table for the Bill of Materials feature
                await CreateTableIfNotExistsAsync(connection, "BomComponents", @"
                    CREATE TABLE BomComponents (
                        Id TEXT NOT NULL PRIMARY KEY,
                        ParentItemId TEXT NOT NULL,
                        ComponentItemId TEXT NOT NULL,
                        QuantityPerUnit TEXT NOT NULL
                    )");
                await CreateIndexIfNotExistsAsync(connection, "IX_BomComponents_ParentItemId", "BomComponents", "ParentItemId");
                await CreateIndexIfNotExistsAsync(connection, "IX_BomComponents_ComponentItemId", "BomComponents", "ComponentItemId");

                Log.Information("Database schema updates completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying database schema updates");
                throw;
            }
        }

        private static async Task CreateTableIfNotExistsAsync(SqliteConnection connection, string tableName, string createSql)
        {
            var checkSql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkSql;
            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

            if (!exists)
            {
                using var createCommand = connection.CreateCommand();
                createCommand.CommandText = createSql;
                await createCommand.ExecuteNonQueryAsync();
                Log.Information("Created table {Table}", tableName);
            }
        }

        private static async Task AddColumnIfNotExistsAsync(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            var checkSql = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkSql;
            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

            if (!exists)
            {
                var alterSql = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = alterSql;
                await alterCommand.ExecuteNonQueryAsync();
                Log.Information("Added column {Column} to table {Table}", columnName, tableName);
            }
            else
            {
                Log.Debug("Column {Column} already exists in table {Table}", columnName, tableName);
            }
        }

        private static async Task CreateIndexIfNotExistsAsync(SqliteConnection connection, string indexName, string tableName, string columns)
        {
            var checkSql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='{indexName}'";

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkSql;
            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

            if (!exists)
            {
                var createSql = $"CREATE INDEX {indexName} ON {tableName}({columns})";
                using var createCommand = connection.CreateCommand();
                createCommand.CommandText = createSql;
                await createCommand.ExecuteNonQueryAsync();
                Log.Information("Created index {Index} on table {Table}", indexName, tableName);
            }
            else
            {
                Log.Debug("Index {Index} already exists", indexName);
            }
        }
    }
}
