using System;

namespace StockAndFlow.Models
{
    public class Customer : ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedDate { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name;
        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
    }
}
