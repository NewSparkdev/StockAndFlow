using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class BusinessSettingsViewModel : ViewModelBase
    {
        private readonly BusinessSettingsService _settingsService;
        private BusinessSettings _settings;

        private string? _businessName;
        private string? _address;
        private string? _city;
        private string? _state;
        private string? _zipCode;
        private string? _phone;
        private string? _email;
        private string? _website;
        private string? _taxId;
        private string? _logoPath;

        public event EventHandler<bool>? CloseRequested;

        public string? BusinessName
        {
            get => _businessName;
            set
            {
                if (SetProperty(ref _businessName, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        public string? State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public string? ZipCode
        {
            get => _zipCode;
            set => SetProperty(ref _zipCode, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string? Website
        {
            get => _website;
            set => SetProperty(ref _website, value);
        }

        public string? TaxId
        {
            get => _taxId;
            set => SetProperty(ref _taxId, value);
        }

        public string? LogoPath
        {
            get => _logoPath;
            set
            {
                if (SetProperty(ref _logoPath, value))
                {
                    OnPropertyChanged(nameof(HasLogo));
                }
            }
        }

        public bool HasLogo => !string.IsNullOrWhiteSpace(LogoPath);

        public bool IsValid => !string.IsNullOrWhiteSpace(BusinessName);

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseLogoCommand { get; }

        public BusinessSettingsViewModel(BusinessSettingsService settingsService)
        {
            _settingsService = settingsService;
            _settings = new BusinessSettings();

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsValid);
            CancelCommand = new RelayCommand(Cancel);
            BrowseLogoCommand = new RelayCommand(BrowseLogo);

            _ = LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                _settings = await _settingsService.GetSettingsAsync();

                // Load values into properties
                BusinessName = _settings.BusinessName;
                Address = _settings.Address;
                City = _settings.City;
                State = _settings.State;
                ZipCode = _settings.ZipCode;
                Phone = _settings.Phone;
                Email = _settings.Email;
                Website = _settings.Website;
                TaxId = _settings.TaxId;
                LogoPath = _settings.LogoPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Update settings with current values
                _settings.BusinessName = BusinessName;
                _settings.Address = Address;
                _settings.City = City;
                _settings.State = State;
                _settings.ZipCode = ZipCode;
                _settings.Phone = Phone;
                _settings.Email = Email;
                _settings.Website = Website;
                _settings.TaxId = TaxId;
                _settings.LogoPath = LogoPath;

                await _settingsService.SaveSettingsAsync(_settings);
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                CloseRequested?.Invoke(this, false);
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }

        private void BrowseLogo()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Business Logo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var imagesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                    if (!Directory.Exists(imagesFolder))
                    {
                        Directory.CreateDirectory(imagesFolder);
                    }

                    var fileName = $"logo_{Guid.NewGuid()}{Path.GetExtension(openFileDialog.FileName)}";
                    var destPath = Path.Combine(imagesFolder, fileName);

                    File.Copy(openFileDialog.FileName, destPath, true);
                    LogoPath = destPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying logo: {ex.Message}");
                    LogoPath = openFileDialog.FileName;
                }
            }
        }
    }
}
