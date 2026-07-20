using Microsoft.EntityFrameworkCore.Design;

namespace StockAndFlow.Data
{
    // Used by EF Core tools (dotnet ef dbcontext optimize) at design time.
    // Not used at runtime.
    public class StockAndFlowDbContextFactory : IDesignTimeDbContextFactory<StockAndFlowDbContext>
    {
        public StockAndFlowDbContext CreateDbContext(string[] args)
        {
            return new StockAndFlowDbContext(":memory:");
        }
    }
}
