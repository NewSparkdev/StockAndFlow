using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockAndFlow.Data;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class CustomerService
    {
        private readonly IDataService _dataService;

        public event EventHandler? CustomerChanged;

        public CustomerService(IDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            return await _dataService.GetAllAsync<Customer>();
        }

        public async Task<Customer?> GetCustomerByIdAsync(Guid id)
        {
            return await _dataService.GetByIdAsync<Customer>(id);
        }

        public async Task<Customer> SaveCustomerAsync(Customer customer)
        {
            customer.LastModifiedDate = DateTime.Now;
            if (customer.Id == Guid.Empty)
            {
                customer.Id = Guid.NewGuid();
                customer.CreatedDate = DateTime.Now;
            }
            await _dataService.SaveAsync(customer);
            CustomerChanged?.Invoke(this, EventArgs.Empty);
            return customer;
        }

        public async Task DeleteCustomerAsync(Guid id)
        {
            var customer = await _dataService.GetByIdAsync<Customer>(id);
            if (customer == null) return;
            customer.IsDeleted = true;
            customer.DeletedDate = DateTime.Now;
            await _dataService.SaveAsync(customer);
            CustomerChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<Customer>> SearchAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllCustomersAsync();
            var lower = searchTerm.ToLowerInvariant();
            var all = await GetAllCustomersAsync();
            return all.Where(c =>
                c.Name.ToLowerInvariant().Contains(lower) ||
                (c.Email != null && c.Email.ToLowerInvariant().Contains(lower)) ||
                (c.Phone != null && c.Phone.Contains(searchTerm))
            ).ToList();
        }
    }
}
