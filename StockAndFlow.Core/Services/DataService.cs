using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    /// <summary>
    /// No-op transaction for JSON-based storage.
    /// NOTE: JSON storage does not support true transactions. All operations
    /// are immediately persisted to disk. This implementation exists for
    /// interface compatibility only.
    /// </summary>
    internal class JsonNoOpTransaction : IDataTransaction
    {
        public Task CommitAsync()
        {
            // No-op: JSON storage commits immediately on each SaveAsync
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            // No-op: Cannot rollback JSON storage (no transaction support)
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    public class DataService : IDataService
    {
        private readonly string _dataDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        // PERFORMANCE: Use LRU cache with size limits to prevent memory issues
        private readonly LruCache<Type, object> _cache;
        private const int MaxCacheSize = 10; // Maximum number of entity types to cache

        // PERFORMANCE: Cache PropertyInfo to avoid repeated reflection calls
        private static readonly Dictionary<Type, System.Reflection.PropertyInfo> _idPropertyCache = new();

        public DataService(string dataDirectory = "Data")
        {
            _dataDirectory = dataDirectory;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            _cache = new LruCache<Type, object>(MaxCacheSize);
        }

        public async Task InitializeAsync()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }

            // Preload all data into memory
            await GetAllAsync<InventoryItem>();
            await GetAllAsync<Sale>();
            await GetAllAsync<Expense>();
        }

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            var type = typeof(T);

            // Try to get from LRU cache
            if (_cache.TryGetValue(type, out var cachedValue))
            {
                return ((List<T>)cachedValue!).ToList();
            }

            var filePath = GetFilePath<T>();

            if (!File.Exists(filePath))
            {
                var emptyList = new List<T>();
                _cache.Set(type, emptyList);
                return emptyList;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var items = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();

            // Add to LRU cache (will evict least recently used if full)
            _cache.Set(type, items);
            return items.ToList();
        }

        public async Task<T?> GetByIdAsync<T>(Guid id) where T : class
        {
            var items = await GetAllAsync<T>();

            // PERFORMANCE: Get cached PropertyInfo instead of reflecting every time
            var type = typeof(T);
            if (!_idPropertyCache.TryGetValue(type, out var idProperty))
            {
                idProperty = type.GetProperty("Id");
                if (idProperty == null)
                    return null;

                _idPropertyCache[type] = idProperty;
            }

            return items.FirstOrDefault(item =>
                (Guid)(idProperty.GetValue(item) ?? Guid.Empty) == id);
        }

        public async Task<List<T>> GetWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            // For JSON mode, we load all data and filter in memory
            // This is acceptable since JSON mode is for small datasets
            var items = await GetAllAsync<T>();
            var compiledPredicate = predicate.Compile();
            return items.Where(compiledPredicate).ToList();
        }

        public async Task SaveAsync<T>(T item) where T : class
        {
            var items = await GetAllAsync<T>();

            // PERFORMANCE: Get cached PropertyInfo instead of reflecting every time
            var type = typeof(T);
            if (!_idPropertyCache.TryGetValue(type, out var idProperty))
            {
                idProperty = type.GetProperty("Id");
                if (idProperty == null)
                    throw new InvalidOperationException("Type must have an Id property");

                _idPropertyCache[type] = idProperty;
            }

            var id = (Guid)(idProperty.GetValue(item) ?? Guid.Empty);
            var existingIndex = items.FindIndex(i =>
                (Guid)(idProperty.GetValue(i) ?? Guid.Empty) == id);

            if (existingIndex >= 0)
            {
                items[existingIndex] = item;
            }
            else
            {
                items.Add(item);
            }

            var filePath = GetFilePath<T>();
            var json = JsonSerializer.Serialize(items, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            // Update LRU cache
            _cache.Set(typeof(T), items);
        }

        public async Task DeleteAsync<T>(Guid id) where T : class
        {
            var items = await GetAllAsync<T>();
            var idProperty = typeof(T).GetProperty("Id");

            if (idProperty == null)
                throw new InvalidOperationException("Type must have an Id property");

            items.RemoveAll(item =>
                (Guid)(idProperty.GetValue(item) ?? Guid.Empty) == id);

            var filePath = GetFilePath<T>();
            var json = JsonSerializer.Serialize(items, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            // Update LRU cache
            _cache.Set(typeof(T), items);
        }

        public async Task<AppSettings> GetSettingsAsync()
        {
            var filePath = Path.Combine(_dataDirectory, "settings.json");

            if (!File.Exists(filePath))
            {
                return new AppSettings();
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            var filePath = Path.Combine(_dataDirectory, "settings.json");
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public Task<IDataTransaction> BeginTransactionAsync()
        {
            // JSON storage doesn't support real transactions
            // Return a no-op transaction for interface compatibility
            IDataTransaction transaction = new JsonNoOpTransaction();
            return Task.FromResult(transaction);
        }

        private string GetFilePath<T>()
        {
            var typeName = typeof(T).Name.ToLower();
            return Path.Combine(_dataDirectory, $"{typeName}s.json");
        }
    }
}
