using System.Collections.Generic;
using System.Linq;

namespace StockAndFlow.Models
{
    public class StateTaxInfo
    {
        public string StateCode { get; set; } = string.Empty;
        public string StateName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; } // As percentage (e.g., 7.5 for 7.5%)

        public string DisplayName => $"{StateName} ({TaxRate:F2}%)";

        public static List<StateTaxInfo> GetAllStates()
        {
            return new List<StateTaxInfo>
            {
                new StateTaxInfo { StateCode = "NONE", StateName = "No Tax", TaxRate = 0 },
                new StateTaxInfo { StateCode = "AL", StateName = "Alabama", TaxRate = 4.0m },
                new StateTaxInfo { StateCode = "AK", StateName = "Alaska", TaxRate = 0 },
                new StateTaxInfo { StateCode = "AZ", StateName = "Arizona", TaxRate = 5.6m },
                new StateTaxInfo { StateCode = "AR", StateName = "Arkansas", TaxRate = 6.5m },
                new StateTaxInfo { StateCode = "CA", StateName = "California", TaxRate = 7.25m },
                new StateTaxInfo { StateCode = "CO", StateName = "Colorado", TaxRate = 2.9m },
                new StateTaxInfo { StateCode = "CT", StateName = "Connecticut", TaxRate = 6.35m },
                new StateTaxInfo { StateCode = "DE", StateName = "Delaware", TaxRate = 0 },
                new StateTaxInfo { StateCode = "FL", StateName = "Florida", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "GA", StateName = "Georgia", TaxRate = 4.0m },
                new StateTaxInfo { StateCode = "HI", StateName = "Hawaii", TaxRate = 4.0m },
                new StateTaxInfo { StateCode = "ID", StateName = "Idaho", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "IL", StateName = "Illinois", TaxRate = 6.25m },
                new StateTaxInfo { StateCode = "IN", StateName = "Indiana", TaxRate = 7.0m },
                new StateTaxInfo { StateCode = "IA", StateName = "Iowa", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "KS", StateName = "Kansas", TaxRate = 6.5m },
                new StateTaxInfo { StateCode = "KY", StateName = "Kentucky", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "LA", StateName = "Louisiana", TaxRate = 4.45m },
                new StateTaxInfo { StateCode = "ME", StateName = "Maine", TaxRate = 5.5m },
                new StateTaxInfo { StateCode = "MD", StateName = "Maryland", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "MA", StateName = "Massachusetts", TaxRate = 6.25m },
                new StateTaxInfo { StateCode = "MI", StateName = "Michigan", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "MN", StateName = "Minnesota", TaxRate = 6.875m },
                new StateTaxInfo { StateCode = "MS", StateName = "Mississippi", TaxRate = 7.0m },
                new StateTaxInfo { StateCode = "MO", StateName = "Missouri", TaxRate = 4.225m },
                new StateTaxInfo { StateCode = "MT", StateName = "Montana", TaxRate = 0 },
                new StateTaxInfo { StateCode = "NE", StateName = "Nebraska", TaxRate = 5.5m },
                new StateTaxInfo { StateCode = "NV", StateName = "Nevada", TaxRate = 6.85m },
                new StateTaxInfo { StateCode = "NH", StateName = "New Hampshire", TaxRate = 0 },
                new StateTaxInfo { StateCode = "NJ", StateName = "New Jersey", TaxRate = 6.625m },
                new StateTaxInfo { StateCode = "NM", StateName = "New Mexico", TaxRate = 5.125m },
                new StateTaxInfo { StateCode = "NY", StateName = "New York", TaxRate = 4.0m },
                new StateTaxInfo { StateCode = "NC", StateName = "North Carolina", TaxRate = 4.75m },
                new StateTaxInfo { StateCode = "ND", StateName = "North Dakota", TaxRate = 5.0m },
                new StateTaxInfo { StateCode = "OH", StateName = "Ohio", TaxRate = 5.75m },
                new StateTaxInfo { StateCode = "OK", StateName = "Oklahoma", TaxRate = 4.5m },
                new StateTaxInfo { StateCode = "OR", StateName = "Oregon", TaxRate = 0 },
                new StateTaxInfo { StateCode = "PA", StateName = "Pennsylvania", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "RI", StateName = "Rhode Island", TaxRate = 7.0m },
                new StateTaxInfo { StateCode = "SC", StateName = "South Carolina", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "SD", StateName = "South Dakota", TaxRate = 4.5m },
                new StateTaxInfo { StateCode = "TN", StateName = "Tennessee", TaxRate = 7.0m },
                new StateTaxInfo { StateCode = "TX", StateName = "Texas", TaxRate = 6.25m },
                new StateTaxInfo { StateCode = "UT", StateName = "Utah", TaxRate = 6.1m },
                new StateTaxInfo { StateCode = "VT", StateName = "Vermont", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "VA", StateName = "Virginia", TaxRate = 5.3m },
                new StateTaxInfo { StateCode = "WA", StateName = "Washington", TaxRate = 6.5m },
                new StateTaxInfo { StateCode = "WV", StateName = "West Virginia", TaxRate = 6.0m },
                new StateTaxInfo { StateCode = "WI", StateName = "Wisconsin", TaxRate = 5.0m },
                new StateTaxInfo { StateCode = "WY", StateName = "Wyoming", TaxRate = 4.0m },
                new StateTaxInfo { StateCode = "DC", StateName = "District of Columbia", TaxRate = 6.0m }
            };
        }

        public static StateTaxInfo? GetByStateCode(string stateCode)
        {
            return GetAllStates().FirstOrDefault(s => s.StateCode == stateCode);
        }
    }
}
