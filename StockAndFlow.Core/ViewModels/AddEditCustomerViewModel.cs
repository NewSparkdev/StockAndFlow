using System;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class AddEditCustomerViewModel : ViewModelBase
    {
        private readonly CustomerService _customerService;
        private Customer _customer = new();

        private string _name = string.Empty;
        private string? _email;
        private string? _phone;
        private string? _address;
        private string? _notes;

        public event EventHandler<bool>? CloseRequested;

        public string DialogTitle => _customer.Id == Guid.Empty ? "New Customer" : "Edit Customer";

        public string Name
        {
            get => _name;
            set { if (SetProperty(ref _name, value)) OnPropertyChanged(nameof(IsValid)); }
        }

        public string? Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(Name);

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddEditCustomerViewModel(CustomerService customerService)
        {
            _customerService = customerService;
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsValid);
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        }

        public void Load(Customer? customer)
        {
            _customer = customer ?? new Customer();
            Name = _customer.Name;
            Email = _customer.Email;
            Phone = _customer.Phone;
            Address = _customer.Address;
            Notes = _customer.Notes;
            OnPropertyChanged(nameof(DialogTitle));
        }

        private async Task SaveAsync()
        {
            try
            {
                _customer.Name = Name.Trim();
                _customer.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
                _customer.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
                _customer.Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim();
                _customer.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
                await _customerService.SaveCustomerAsync(_customer);
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to save customer");
            }
        }
    }
}
