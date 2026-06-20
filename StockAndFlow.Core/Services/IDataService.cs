using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    /// <summary>
    /// Represents a database transaction scope
    /// </summary>
    public interface IDataTransaction : IDisposable
    {
        /// <summary>
        /// Commits all changes made within the transaction scope
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rolls back all changes made within the transaction scope
        /// </summary>
        Task RollbackAsync();
    }

    public interface IDataService
    {
        Task InitializeAsync();

        // Generic CRUD operations
        Task<List<T>> GetAllAsync<T>() where T : class;
        Task<T?> GetByIdAsync<T>(Guid id) where T : class;
        Task SaveAsync<T>(T item) where T : class;
        Task DeleteAsync<T>(Guid id) where T : class;

        // Query operations (performance optimized)
        /// <summary>
        /// Gets entities filtered by a predicate. For database-backed implementations,
        /// this will translate to a SQL WHERE clause for better performance.
        /// </summary>
        Task<List<T>> GetWhereAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class;

        // Settings
        Task<AppSettings> GetSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);

        // Transaction support
        /// <summary>
        /// Begins a database transaction. All operations within the transaction
        /// will be committed atomically when CommitAsync is called, or rolled back
        /// if Dispose is called without committing.
        /// </summary>
        /// <returns>A transaction object that must be disposed</returns>
        Task<IDataTransaction> BeginTransactionAsync();
    }
}
