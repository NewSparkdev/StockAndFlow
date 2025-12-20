using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class DataService : IDataService
    {
        private readonly string _dataDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        private Dictionary<Type, object> _cache = new();

        public DataService(string dataDirectory = "Data")
        {
            _dataDirectory = dataDirectory;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
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

            if (_cache.ContainsKey(type))
            {
                return ((List<T>)_cache[type]).ToList();
            }

            var filePath = GetFilePath<T>();

            if (!File.Exists(filePath))
            {
                var emptyList = new List<T>();
                _cache[type] = emptyList;
                return emptyList;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var items = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();

            _cache[type] = items;
            return items.ToList();
        }

        public async Task<T?> GetByIdAsync<T>(Guid id) where T : class
        {
            var items = await GetAllAsync<T>();
            var idProperty = typeof(T).GetProperty("Id");

            if (idProperty == null)
                return null;

            return items.FirstOrDefault(item =>
                (Guid)(idProperty.GetValue(item) ?? Guid.Empty) == id);
        }

        public async Task SaveAsync<T>(T item) where T : class
        {
            var items = await GetAllAsync<T>();
            var idProperty = typeof(T).GetProperty("Id");

            if (idProperty == null)
                throw new InvalidOperationException("Type must have an Id property");

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

            // Update cache
            _cache[typeof(T)] = items;
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

            // Update cache
            _cache[typeof(T)] = items;
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

        private string GetFilePath<T>()
        {
            var typeName = typeof(T).Name.ToLower();
            return Path.Combine(_dataDirectory, $"{typeName}s.json");
        }
    }
}
