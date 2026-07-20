using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StockAndFlow.Models;
using StockAndFlow.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace StockAndFlow.Data
{
    /// <summary>
    /// EF Core transaction wrapper
    /// </summary>
    internal class EfCoreTransaction : IDataTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private bool _disposed;

        public EfCoreTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        public async Task CommitAsync()
        {
            await _transaction.CommitAsync();
        }

        public async Task RollbackAsync()
        {
            await _transaction.RollbackAsync();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _transaction?.Dispose();
                _disposed = true;
            }
        }
    }

    public class SQLiteDataService : IDataService
    {
        private readonly StockAndFlowDbContext _context;
        private readonly string _settingsFilePath;
        private AppSettings? _cachedSettings;

        // PERFORMANCE: Cache PropertyInfo to avoid repeated reflection calls
        private static readonly Dictionary<Type, System.Reflection.PropertyInfo> _idPropertyCache = new();

        public SQLiteDataService(string databasePath, string settingsPath)
        {
            _context = new StockAndFlowDbContext(databasePath);
            _settingsFilePath = settingsPath;
        }

        public async Task InitializeAsync()
        {
            // Ensure database is created
            await Task.Run(() => _context.EnsureDatabaseCreated());

            // Apply schema updates for existing databases
            var databasePath = _context.Database.GetConnectionString()?.Replace("Data Source=", "");
            if (!string.IsNullOrEmpty(databasePath))
            {
                await DatabaseMigrationHelper.ApplySchemaUpdatesAsync(databasePath);
            }

            // Ensure settings file directory exists
            var settingsDir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            // Initialize settings file if it doesn't exist
            if (!File.Exists(_settingsFilePath))
            {
                var defaultSettings = new AppSettings();
                var json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                });
                File.WriteAllText(_settingsFilePath, json);
            }
        }

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
                return await _context.Set<T>()
                    .Where(e => !EF.Property<bool>(e, "IsDeleted"))
                    .ToListAsync();

            return await _context.Set<T>().ToListAsync();
        }

        public async Task<T?> GetByIdAsync<T>(Guid id) where T : class
        {
            return await _context.Set<T>().FindAsync(id);
        }

        public async Task<List<T>> GetWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            // EF Core translates this to SQL WHERE clause for optimal performance
            return await _context.Set<T>().Where(predicate).ToListAsync();
        }

        public async Task SaveAsync<T>(T item) where T : class
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            // PERFORMANCE: Get cached PropertyInfo instead of reflecting every time
            var type = typeof(T);
            if (!_idPropertyCache.TryGetValue(type, out var idProperty))
            {
                idProperty = type.GetProperty("Id");
                if (idProperty == null)
                    throw new InvalidOperationException($"Type {type.Name} does not have an Id property");

                _idPropertyCache[type] = idProperty;
            }

            var id = (Guid?)idProperty.GetValue(item);
            if (id == null || id == Guid.Empty)
                throw new InvalidOperationException($"Item of type {type.Name} has an invalid Id");

            var existingItem = await _context.Set<T>().FindAsync(id);

            if (existingItem == null)
            {
                // Add new item
                await _context.Set<T>().AddAsync(item);
            }
            else
            {
                // Update existing item
                _context.Entry(existingItem).CurrentValues.SetValues(item);
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync<T>(Guid id) where T : class
        {
            var item = await _context.Set<T>().FindAsync(id);
            if (item != null)
            {
                _context.Set<T>().Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<AppSettings> GetSettingsAsync()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

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
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                });

                File.WriteAllText(_settingsFilePath, json);
                _cachedSettings = settings;
            });
        }

        public async Task<IDataTransaction> BeginTransactionAsync()
        {
            var transaction = await _context.Database.BeginTransactionAsync();
            return new EfCoreTransaction(transaction);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
