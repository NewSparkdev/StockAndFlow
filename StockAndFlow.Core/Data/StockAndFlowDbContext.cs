using Microsoft.EntityFrameworkCore;
using StockAndFlow.Models;
using System;

namespace StockAndFlow.Data
{
    public class StockAndFlowDbContext : DbContext
    {
        private readonly string _databasePath;

        public DbSet<InventoryItem> InventoryItems { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; } = null!;
        public DbSet<BusinessSettings> BusinessSettings { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<BomComponent> BomComponents { get; set; } = null!;

        public StockAndFlowDbContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_databasePath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure InventoryItem
            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.CostPerUnit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SalePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedDate).IsRequired();
                entity.Property(e => e.LastModifiedDate).IsRequired();

                // Soft delete configuration
                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.DeletedDate).IsRequired(false);

                // Global query filter: Exclude soft-deleted items by default
                entity.HasQueryFilter(e => !e.IsDeleted);

                // Add index on Name for faster searching
                entity.HasIndex(e => e.Name);

                // Add index on SKU for lookups
                entity.HasIndex(e => e.Sku);

                // Add index on Category for filtering
                entity.HasIndex(e => e.Category);

                // Add index on ShopifyProductId for sync operations
                entity.HasIndex(e => e.ShopifyProductId);

                // Add index on IsDeleted for filtering soft-deleted items
                entity.HasIndex(e => e.IsDeleted);

                // Ignore calculated properties
                entity.Ignore(e => e.TotalValue);
                entity.Ignore(e => e.ProfitPerUnit);
                entity.Ignore(e => e.ProfitMarginPerUnit);
                entity.Ignore(e => e.ProfitMarginPercentage);
                entity.Ignore(e => e.HasImage);
                entity.Ignore(e => e.IsSyncedWithShopify);
                entity.Ignore(e => e.Description);
                entity.Ignore(e => e.IsOutOfStock);
                entity.Ignore(e => e.IsLowStock);
                entity.Ignore(e => e.IsInStock);
                entity.Ignore(e => e.StockStatus);
                entity.Ignore(e => e.StockStatusColor);
            });

            // Configure Sale
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Configure TransactionId to use the backing field
                entity.Property(e => e.TransactionId)
                    .HasField("_transactionId")
                    .IsRequired();

                entity.Property(e => e.SaleDate).IsRequired();
                entity.Property(e => e.InventoryItemId).IsRequired();
                entity.Property(e => e.ItemName).IsRequired();
                entity.Property(e => e.SalePricePerUnit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CostPerUnit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");

                // FOREIGN KEY: Configure relationship with InventoryItem
                // Restrict delete to prevent orphaned sales (must delete sales first)
                entity.HasOne<InventoryItem>()
                    .WithMany()
                    .HasForeignKey(e => e.InventoryItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                // FOREIGN KEY: Optional relationship with Customer
                entity.Property(e => e.CustomerId);
                entity.HasOne<Customer>()
                      .WithMany()
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                // INDEXES: Add indexes on frequently queried fields
                entity.HasIndex(e => e.TransactionId)
                    .HasDatabaseName("IX_Sales_TransactionId");

                entity.HasIndex(e => e.SaleDate)
                    .HasDatabaseName("IX_Sales_SaleDate");

                entity.HasIndex(e => e.InventoryItemId)
                    .HasDatabaseName("IX_Sales_InventoryItemId");

                entity.HasIndex(e => e.ShopifyOrderId)
                    .HasDatabaseName("IX_Sales_ShopifyOrderId");

                // Composite index for date range queries by item
                entity.HasIndex(e => new { e.InventoryItemId, e.SaleDate })
                    .HasDatabaseName("IX_Sales_InventoryItemId_SaleDate");

                // Ignore calculated properties
                entity.Ignore(e => e.Subtotal);
                entity.Ignore(e => e.Revenue);
                entity.Ignore(e => e.COGS);
                entity.Ignore(e => e.Profit);
                entity.Ignore(e => e.ProfitMargin);
            });

            // Configure Expense
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ExpenseDate).IsRequired();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Category).IsRequired();
                entity.Property(e => e.CreatedDate).IsRequired();

                // FOREIGN KEY: Configure optional relationship with InventoryItem
                // SetNull on delete to preserve expense record even if item deleted
                entity.HasOne<InventoryItem>()
                    .WithMany()
                    .HasForeignKey(e => e.InventoryItemId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                // INDEXES: Add indexes on frequently queried fields
                entity.HasIndex(e => e.ExpenseDate)
                    .HasDatabaseName("IX_Expenses_ExpenseDate");

                entity.HasIndex(e => e.Category)
                    .HasDatabaseName("IX_Expenses_Category");

                entity.HasIndex(e => e.InventoryItemId)
                    .HasDatabaseName("IX_Expenses_InventoryItemId");

                // Composite index for date range queries by category
                entity.HasIndex(e => new { e.Category, e.ExpenseDate })
                    .HasDatabaseName("IX_Expenses_Category_ExpenseDate");

                // Ignore calculated properties
                entity.Ignore(e => e.HasReceipt);
                entity.Ignore(e => e.HasLinkedItem);
                entity.Ignore(e => e.LinkedInventoryItemId);
            });

            // Configure InventoryAdjustment
            modelBuilder.Entity<InventoryAdjustment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InventoryItemId).IsRequired();
                entity.Property(e => e.InventoryItemName).IsRequired();
                entity.Property(e => e.Reason).IsRequired().HasConversion<string>();
                entity.Property(e => e.AdjustmentDate).IsRequired();
                entity.Property(e => e.CreatedDate).IsRequired();
                entity.Property(e => e.CostPerUnit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SalePricePerUnit).HasColumnType("decimal(18,2)");

                // FOREIGN KEY: Configure relationship with InventoryItem
                // Restrict delete to preserve adjustment history
                entity.HasOne<InventoryItem>()
                    .WithMany()
                    .HasForeignKey(e => e.InventoryItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                // INDEXES: Add indexes on frequently queried fields
                entity.HasIndex(e => e.AdjustmentDate)
                    .HasDatabaseName("IX_InventoryAdjustments_AdjustmentDate");

                entity.HasIndex(e => e.InventoryItemId)
                    .HasDatabaseName("IX_InventoryAdjustments_InventoryItemId");

                entity.HasIndex(e => e.Reason)
                    .HasDatabaseName("IX_InventoryAdjustments_Reason");

                // Composite index for date range queries by item
                entity.HasIndex(e => new { e.InventoryItemId, e.AdjustmentDate })
                    .HasDatabaseName("IX_InventoryAdjustments_InventoryItemId_AdjustmentDate");

                // Ignore calculated properties
                entity.Ignore(e => e.TotalCost);
                entity.Ignore(e => e.PotentialRevenue);
                entity.Ignore(e => e.PotentialProfit);
                entity.Ignore(e => e.IsLoss);
                entity.Ignore(e => e.ReasonDisplay);
                entity.Ignore(e => e.QuantityChangeDisplay);
                entity.Ignore(e => e.QuantityChangeColor);
            });

            // Configure BusinessSettings
            modelBuilder.Entity<BusinessSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LastModified).IsRequired();

                // Ignore calculated properties
                entity.Ignore(e => e.FullAddress);
            });

            // Configure Customer
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Email);
                entity.Ignore(e => e.DisplayName);
                entity.Ignore(e => e.HasEmail);
                entity.Ignore(e => e.HasPhone);
            });

            // Configure BomComponent
            modelBuilder.Entity<BomComponent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ParentItemId).IsRequired();
                entity.Property(e => e.ComponentItemId).IsRequired();
                entity.Property(e => e.QuantityPerUnit).HasColumnType("decimal(18,4)");

                entity.HasOne<InventoryItem>()
                    .WithMany()
                    .HasForeignKey(e => e.ParentItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                entity.HasOne<InventoryItem>()
                    .WithMany()
                    .HasForeignKey(e => e.ComponentItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                entity.HasIndex(e => e.ParentItemId)
                    .HasDatabaseName("IX_BomComponents_ParentItemId");

                entity.HasIndex(e => e.ComponentItemId)
                    .HasDatabaseName("IX_BomComponents_ComponentItemId");
            });
        }

        public void EnsureDatabaseCreated()
        {
            Database.EnsureCreated();
        }
    }
}
