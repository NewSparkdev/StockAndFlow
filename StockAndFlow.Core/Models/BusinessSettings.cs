using System;
using System.Linq;

namespace StockAndFlow.Models
{
    public class BusinessSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? BusinessName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? TaxId { get; set; }
        public string? DefaultTaxStateCode { get; set; }
        public string? LogoPath { get; set; }
        public DateTime LastModified { get; set; } = DateTime.Now;

        // Invoice display choices: what appears on generated customer invoices.
        // All default to true so existing invoices keep looking the same; a field also
        // needs a value to print (blank fields are always skipped).
        public bool ShowLogoOnInvoice { get; set; } = true;
        public bool ShowPhoneOnInvoice { get; set; } = true;
        public bool ShowEmailOnInvoice { get; set; } = true;
        public bool ShowWebsiteOnInvoice { get; set; } = true;
        public bool ShowTaxIdOnInvoice { get; set; } = true;

        // Helper property for full address
        public string FullAddress
        {
            get
            {
                var parts = new[]
                {
                    Address,
                    string.IsNullOrWhiteSpace(City) || string.IsNullOrWhiteSpace(State) || string.IsNullOrWhiteSpace(ZipCode)
                        ? null
                        : $"{City}, {State} {ZipCode}"
                };
                return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }
    }
}
