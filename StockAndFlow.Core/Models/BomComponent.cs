using System;

namespace StockAndFlow.Models
{
    public class BomComponent
    {
        public Guid Id { get; set; }
        public Guid ParentItemId { get; set; }
        public Guid ComponentItemId { get; set; }

        // How many units of the component are consumed per 1 unit of the finished good sold.
        public decimal QuantityPerUnit { get; set; }

        public BomComponent()
        {
            Id = Guid.NewGuid();
        }
    }
}
