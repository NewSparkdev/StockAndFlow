using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using StockAndFlow.Commands;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;

namespace StockAndFlow.ViewModels
{
    public class CustomersViewModel : ViewModelBase
    {
        private readonly CustomerService _customerService;
        private ObservableCollection<Customer> _customers = new();
        private string _searchText = string.Empty;
        private bool _isLoading;

        public event EventHandler<Customer?>? AddEditRequested;
        public event EventHandler<Customer>? DetailRequested;

        public ObservableCollection<Customer> Customers
        {
            get => _customers;
            set => SetProperty(ref _customers, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    _ = SearchAsync();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SelectCommand { get; }

        public CustomersViewModel(CustomerService customerService)
        {
            _customerService = customerService;
            _customerService.CustomerChanged += async (_, _) => await LoadAsync();

            AddCommand = new RelayCommand(() => AddEditRequested?.Invoke(this, null));
            EditCommand = new RelayCommand<Customer>(c => AddEditRequested?.Invoke(this, c));
            DeleteCommand = new RelayCommand<Customer>(async c => await DeleteAsync(c!));
            SelectCommand = new RelayCommand<Customer>(c => DetailRequested?.Invoke(this, c!));

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                var list = string.IsNullOrWhiteSpace(SearchText)
                    ? await _customerService.GetAllCustomersAsync()
                    : await _customerService.SearchAsync(SearchText);
                UiDispatcher.Run(() => Customers = new ObservableCollection<Customer>(list.OrderBy(c => c.Name)));
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load customers");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SearchAsync()
        {
            await LoadAsync();
        }

        private async Task DeleteAsync(Customer customer)
        {
            try
            {
                await _customerService.DeleteCustomerAsync(customer.Id);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to delete customer");
            }
        }
    }
}
