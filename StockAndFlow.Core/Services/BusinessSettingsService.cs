using System;
using System.Linq;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class BusinessSettingsService
    {
        private readonly IDataService _dataService;

        public event EventHandler? SettingsChanged;

        public BusinessSettingsService(IDataService dataService)
        {
            _dataService = dataService;
        }

        /// <summary>
        /// Gets the business settings. If none exist, creates a new empty one.
        /// </summary>
        public async Task<BusinessSettings> GetSettingsAsync()
        {
            var allSettings = await _dataService.GetAllAsync<BusinessSettings>();
            var settings = allSettings.FirstOrDefault();

            if (settings == null)
            {
                // Create default settings
                settings = new BusinessSettings();
                await SaveSettingsAsync(settings);
            }

            return settings;
        }

        /// <summary>
        /// Saves or updates the business settings.
        /// </summary>
        public async Task<BusinessSettings> SaveSettingsAsync(BusinessSettings settings)
        {
            settings.LastModified = DateTime.Now;
            await _dataService.SaveAsync(settings);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return settings;
        }

        /// <summary>
        /// Checks if business settings have been configured.
        /// </summary>
        public async Task<bool> IsConfiguredAsync()
        {
            var settings = await GetSettingsAsync();
            return !string.IsNullOrWhiteSpace(settings.BusinessName);
        }
    }
}
