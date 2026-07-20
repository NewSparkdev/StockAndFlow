using Microsoft.Data.Sqlite;
using Serilog;
using StockAndFlow.Models;
using StockAndFlow.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace StockAndFlow.Data
{
    internal class SqliteTransactionWrapper : IDataTransaction
    {
        private readonly SqliteTransaction _tx;
        private readonly Action _onComplete;
        private bool _disposed;

        public SqliteTransactionWrapper(SqliteTransaction tx, Action onComplete)
        {
            _tx = tx;
            _onComplete = onComplete;
        }

        public Task CommitAsync()
        {
            _tx.Commit();
            _onComplete();
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            _tx.Rollback();
            _onComplete();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _tx.Dispose();
                _onComplete();
            }
        }
    }

    public class SQLiteDataService : IDataService, IDisposable
    {
        private readonly string _databasePath;
        private readonly string _settingsFilePath;
        private AppSettings? _cachedSettings;

        private SqliteConnection? _connection;
        private SqliteTransaction? _currentTransaction;

        private static readonly Dictionary<Type, string> _tableNames = new()
        {
            { typeof(InventoryItem), "InventoryItems" },
            { typeof(Sale), "Sales" },
            { typeof(Expense), "Expenses" },
            { typeof(InventoryAdjustment), "InventoryAdjustments" },
            { typeof(BusinessSettings), "BusinessSettings" },
            { typeof(Customer), "Customers" },
        };

        private static readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new();

        public SQLiteDataService(string databasePath, string settingsPath)
        {
            _databasePath = databasePath;
            _settingsFilePath = settingsPath;
        }

        private SqliteConnection GetConnection()
        {
            if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
            {
                _connection?.Dispose();
                _connection = new SqliteConnection($"Data Source={_databasePath}");
                _connection.Open();
            }
            return _connection;
        }

        private SqliteCommand CreateCommand(string sql)
        {
            var cmd = GetConnection().CreateCommand();
            cmd.CommandText = sql;
            if (_currentTransaction != null)
                cmd.Transaction = _currentTransaction;
            return cmd;
        }

        private static PropertyInfo[] GetProperties(Type type)
        {
            if (_propertyCache.TryGetValue(type, out var cached))
                return cached;

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetSetMethod(false) != null)
                .ToArray();

            _propertyCache[type] = props;
            return props;
        }

        private static string GetTableName<T>() where T : class
        {
            if (!_tableNames.TryGetValue(typeof(T), out var name))
                throw new InvalidOperationException($"No table registered for type {typeof(T).Name}");
            return name;
        }

        private static object? ToSqliteValue(object? value) => value switch
        {
            null => DBNull.Value,
            Guid g => g.ToString(),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b ? 1 : 0,
            Enum e => e.ToString(),
            _ => value
        };

        private static void SetFromDb(object obj, PropertyInfo prop, object? dbValue)
        {
            if (dbValue == null || dbValue is DBNull)
            {
                if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                    prop.SetValue(obj, null);
                return;
            }

            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var str = Convert.ToString(dbValue, CultureInfo.InvariantCulture)!;

            object? converted = underlying switch
            {
                Type t when t == typeof(Guid) => Guid.Parse(str),
                Type t when t == typeof(DateTime) => DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Type t when t == typeof(decimal) => decimal.Parse(str, CultureInfo.InvariantCulture),
                Type t when t == typeof(bool) => dbValue is long l ? l != 0 : !str.Equals("0") && !str.Equals("false", StringComparison.OrdinalIgnoreCase),
                Type t when t == typeof(int) => Convert.ToInt32(dbValue),
                Type t when t == typeof(long) => Convert.ToInt64(dbValue),
                Type t when t.IsEnum => Enum.Parse(underlying, str),
                _ => Convert.ChangeType(dbValue, underlying, CultureInfo.InvariantCulture)
            };

            prop.SetValue(obj, converted);
        }

        private static T MapRow<T>(SqliteDataReader reader) where T : class
        {
            var obj = (T)Activator.CreateInstance(typeof(T))!;
            var props = GetProperties(typeof(T));

            var fieldMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                fieldMap[reader.GetName(i)] = i;

            foreach (var prop in props)
            {
                if (!fieldMap.TryGetValue(prop.Name, out var ordinal)) continue;
                var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                SetFromDb(obj, prop, value);
            }
            return obj;
        }

        public async Task InitializeAsync()
        {
            var dir = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await CreateTablesAsync();
            await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(_databasePath);

            var settingsDir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(settingsDir))
                Directory.CreateDirectory(settingsDir);

            if (!File.Exists(_settingsFilePath))
            {
                var json = JsonSerializer.Serialize(new AppSettings(), new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
        }

        private async Task CreateTablesAsync()
        {
            var statements = new[]
            {
                @"CREATE TABLE IF NOT EXISTS Customers (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT,
                    Phone TEXT,
                    Address TEXT,
                    Notes TEXT,
                    CreatedDate TEXT NOT NULL,
                    LastModifiedDate TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    DeletedDate TEXT
                )",
                @"CREATE TABLE IF NOT EXISTS InventoryItems (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Sku TEXT,
                    Category TEXT,
                    CostPerUnit TEXT NOT NULL,
                    SalePrice TEXT NOT NULL,
                    QuantityOnHand INTEGER NOT NULL,
                    MinimumStockLevel INTEGER NOT NULL,
                    Supplier TEXT,
                    Notes TEXT,
                    ImagePath TEXT,
                    CreatedDate TEXT NOT NULL,
                    LastModifiedDate TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    DeletedDate TEXT,
                    ShopifyProductId TEXT,
                    ShopifyVariantId TEXT,
                    LastSyncedAt TEXT
                )",
                @"CREATE TABLE IF NOT EXISTS Sales (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TransactionId TEXT NOT NULL,
                    SaleDate TEXT NOT NULL,
                    InventoryItemId TEXT NOT NULL,
                    ItemName TEXT NOT NULL,
                    Quantity INTEGER NOT NULL,
                    SalePricePerUnit TEXT NOT NULL,
                    CostPerUnit TEXT NOT NULL,
                    ShopifyOrderId TEXT,
                    ShopifyOrderNumber TEXT,
                    CustomerName TEXT,
                    CustomerEmail TEXT,
                    CustomerId TEXT,
                    Notes TEXT,
                    TaxStateCode TEXT,
                    TaxRate TEXT NOT NULL,
                    TaxAmount TEXT NOT NULL
                )",
                @"CREATE TABLE IF NOT EXISTS Expenses (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ExpenseDate TEXT NOT NULL,
                    Amount TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Description TEXT,
                    InventoryItemId TEXT,
                    LinkedItemName TEXT,
                    ReceiptImagePath TEXT,
                    CreatedDate TEXT NOT NULL
                )",
                @"CREATE TABLE IF NOT EXISTS InventoryAdjustments (
                    Id TEXT NOT NULL PRIMARY KEY,
                    InventoryItemId TEXT NOT NULL,
                    InventoryItemName TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    QuantityChange INTEGER NOT NULL,
                    CostPerUnit TEXT NOT NULL,
                    SalePricePerUnit TEXT NOT NULL,
                    Notes TEXT,
                    AdjustmentDate TEXT NOT NULL,
                    CreatedDate TEXT NOT NULL
                )",
                @"CREATE TABLE IF NOT EXISTS BusinessSettings (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BusinessName TEXT,
                    Address TEXT,
                    City TEXT,
                    State TEXT,
                    ZipCode TEXT,
                    Phone TEXT,
                    Email TEXT,
                    Website TEXT,
                    TaxId TEXT,
                    DefaultTaxStateCode TEXT,
                    LogoPath TEXT,
                    LastModified TEXT NOT NULL
                )",
            };

            foreach (var sql in statements)
            {
                using var cmd = CreateCommand(sql);
                await cmd.ExecuteNonQueryAsync();
            }

            Log.Information("Database schema initialized");
        }

        private async Task<List<T>> QueryAllAsync<T>() where T : class
        {
            var table = GetTableName<T>();
            using var cmd = CreateCommand($"SELECT * FROM \"{table}\"");
            using var reader = await cmd.ExecuteReaderAsync();
            var result = new List<T>();
            while (await reader.ReadAsync())
                result.Add(MapRow<T>(reader));
            return result;
        }

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            var table = GetTableName<T>();
            var sql = typeof(ISoftDeletable).IsAssignableFrom(typeof(T))
                ? $"SELECT * FROM \"{table}\" WHERE IsDeleted = 0"
                : $"SELECT * FROM \"{table}\"";

            using var cmd = CreateCommand(sql);
            using var reader = await cmd.ExecuteReaderAsync();
            var result = new List<T>();
            while (await reader.ReadAsync())
                result.Add(MapRow<T>(reader));
            return result;
        }

        public async Task<T?> GetByIdAsync<T>(Guid id) where T : class
        {
            var table = GetTableName<T>();
            using var cmd = CreateCommand($"SELECT * FROM \"{table}\" WHERE Id = @id");
            cmd.Parameters.AddWithValue("@id", id.ToString());
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapRow<T>(reader) : null;
        }

        public async Task<List<T>> GetWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            // Load all rows (no IsDeleted filter) then filter in memory.
            // This allows callers to query deleted items as well as active ones.
            var all = await QueryAllAsync<T>();
            var fn = predicate.Compile();
            return all.Where(fn).ToList();
        }

        public async Task SaveAsync<T>(T item) where T : class
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var table = GetTableName<T>();
            var props = GetProperties(typeof(T));
            var cols = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
            var parms = string.Join(", ", props.Select(p => $"@{p.Name}"));

            using var cmd = CreateCommand($"INSERT OR REPLACE INTO \"{table}\" ({cols}) VALUES ({parms})");
            foreach (var prop in props)
                cmd.Parameters.AddWithValue($"@{prop.Name}", ToSqliteValue(prop.GetValue(item)));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync<T>(Guid id) where T : class
        {
            var table = GetTableName<T>();
            using var cmd = CreateCommand($"DELETE FROM \"{table}\" WHERE Id = @id");
            cmd.Parameters.AddWithValue("@id", id.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<AppSettings> GetSettingsAsync()
        {
            if (_cachedSettings != null) return _cachedSettings;

            return await Task.Run(() =>
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _cachedSettings = new AppSettings();
                    return _cachedSettings;
                }

                var json = File.ReadAllText(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AppSettings();

                return _cachedSettings;
            });
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            await Task.Run(() =>
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                _cachedSettings = settings;
            });
        }

        public Task<IDataTransaction> BeginTransactionAsync()
        {
            var tx = GetConnection().BeginTransaction();
            _currentTransaction = tx;
            IDataTransaction wrapper = new SqliteTransactionWrapper(tx, () => _currentTransaction = null);
            return Task.FromResult(wrapper);
        }

        public void Dispose()
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
            _connection?.Dispose();
            _connection = null;
        }
    }
}
