using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public interface IDataService
    {
        Task InitializeAsync();

        // Generic CRUD operations
        Task<List<T>> GetAllAsync<T>() where T : class;
        Task<T?> GetByIdAsync<T>(Guid id) where T : class;
        Task SaveAsync<T>(T item) where T : class;
        Task DeleteAsync<T>(Guid id) where T : class;

        // Settings
        Task<AppSettings> GetSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
    }
}
