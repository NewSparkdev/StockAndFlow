using StockAndFlow.Models;
using StockAndFlow.Services;
using System.Linq.Expressions;

namespace StockAndFlow.Tests.TestUtilities;

/// <summary>
/// In-memory implementation of IDataService for testing purposes.
/// Provides realistic behavior without file I/O or database dependencies.
/// </summary>
public class InMemoryDataService : IDataService
{
    private readonly Dictionary<Type, Dictionary<Guid, object>> _store = new();
    private AppSettings _settings = new AppSettings
    {
        AllowNegativeInventory = false,
        StorageMode = StorageMode.SQLite // Default to SQLite mode for testing
    };
    private bool _initialized = false;

    public InMemoryDataService()
    {
        // Initialize storage for common types
        _store[typeof(InventoryItem)] = new Dictionary<Guid, object>();
        _store[typeof(Sale)] = new Dictionary<Guid, object>();
        _store[typeof(Expense)] = new Dictionary<Guid, object>();
        _store[typeof(InventoryAdjustment)] = new Dictionary<Guid, object>();
        _store[typeof(BusinessSettings)] = new Dictionary<Guid, object>();
    }

    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    public Task<List<T>> GetAllAsync<T>() where T : class
    {
        EnsureInitialized();
        var type = typeof(T);

        if (!_store.ContainsKey(type))
        {
            _store[type] = new Dictionary<Guid, object>();
        }

        var items = _store[type].Values.Cast<T>().ToList();
        return Task.FromResult(items);
    }

    public Task<T?> GetByIdAsync<T>(Guid id) where T : class
    {
        EnsureInitialized();
        var type = typeof(T);

        if (!_store.ContainsKey(type))
        {
            return Task.FromResult<T?>(null);
        }

        if (_store[type].TryGetValue(id, out var item))
        {
            return Task.FromResult<T?>(item as T);
        }

        return Task.FromResult<T?>(null);
    }

    public Task<List<T>> GetWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        EnsureInitialized();
        var type = typeof(T);

        if (!_store.ContainsKey(type))
        {
            return Task.FromResult(new List<T>());
        }

        var items = _store[type].Values.Cast<T>();
        var compiledPredicate = predicate.Compile();
        var filteredItems = items.Where(compiledPredicate).ToList();

        return Task.FromResult(filteredItems);
    }

    public Task SaveAsync<T>(T item) where T : class
    {
        EnsureInitialized();
        var type = typeof(T);

        if (!_store.ContainsKey(type))
        {
            _store[type] = new Dictionary<Guid, object>();
        }

        // Get the Id property using reflection
        var idProperty = type.GetProperty("Id");
        if (idProperty == null)
        {
            throw new InvalidOperationException($"Type {type.Name} does not have an Id property");
        }

        var id = (Guid)idProperty.GetValue(item)!;

        // If Id is empty, generate a new one
        if (id == Guid.Empty)
        {
            id = Guid.NewGuid();
            idProperty.SetValue(item, id);
        }

        _store[type][id] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(Guid id) where T : class
    {
        EnsureInitialized();
        var type = typeof(T);

        if (_store.ContainsKey(type))
        {
            _store[type].Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task<AppSettings> GetSettingsAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_settings);
    }

    public Task SaveSettingsAsync(AppSettings settings)
    {
        EnsureInitialized();
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task<IDataTransaction> BeginTransactionAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IDataTransaction>(new InMemoryTransaction(this));
    }

    /// <summary>
    /// Clears all data from the in-memory store. Useful for resetting state between tests.
    /// </summary>
    public void Reset()
    {
        foreach (var store in _store.Values)
        {
            store.Clear();
        }

        _settings = new AppSettings
        {
            AllowNegativeInventory = false,
            StorageMode = StorageMode.SQLite
        };
    }

    /// <summary>
    /// Gets the current count of items of a specific type. Useful for test assertions.
    /// </summary>
    public int GetCount<T>() where T : class
    {
        var type = typeof(T);
        if (!_store.ContainsKey(type))
        {
            return 0;
        }
        return _store[type].Count;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("DataService has not been initialized. Call InitializeAsync() first.");
        }
    }

    /// <summary>
    /// Creates a snapshot of the current state for transaction rollback support.
    /// </summary>
    internal Dictionary<Type, Dictionary<Guid, object>> CreateSnapshot()
    {
        var snapshot = new Dictionary<Type, Dictionary<Guid, object>>();
        foreach (var kvp in _store)
        {
            snapshot[kvp.Key] = new Dictionary<Guid, object>(kvp.Value);
        }
        return snapshot;
    }

    /// <summary>
    /// Restores the state from a snapshot for transaction rollback.
    /// </summary>
    internal void RestoreSnapshot(Dictionary<Type, Dictionary<Guid, object>> snapshot)
    {
        _store.Clear();
        foreach (var kvp in snapshot)
        {
            _store[kvp.Key] = new Dictionary<Guid, object>(kvp.Value);
        }
    }

    /// <summary>
    /// In-memory transaction implementation that supports commit and rollback.
    /// </summary>
    private class InMemoryTransaction : IDataTransaction
    {
        private readonly InMemoryDataService _dataService;
        private readonly Dictionary<Type, Dictionary<Guid, object>> _snapshot;
        private bool _committed = false;
        private bool _rolledBack = false;
        private bool _disposed = false;

        public InMemoryTransaction(InMemoryDataService dataService)
        {
            _dataService = dataService;
            _snapshot = dataService.CreateSnapshot();
        }

        public Task CommitAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InMemoryTransaction));
            }

            if (_rolledBack)
            {
                throw new InvalidOperationException("Transaction has already been rolled back");
            }

            _committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InMemoryTransaction));
            }

            if (_committed)
            {
                throw new InvalidOperationException("Transaction has already been committed");
            }

            _dataService.RestoreSnapshot(_snapshot);
            _rolledBack = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // If transaction was not explicitly committed or rolled back, rollback on dispose
            if (!_committed && !_rolledBack)
            {
                _dataService.RestoreSnapshot(_snapshot);
            }

            _disposed = true;
        }
    }
}
