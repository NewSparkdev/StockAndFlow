namespace StockAndFlow.Models
{
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
    }
}
