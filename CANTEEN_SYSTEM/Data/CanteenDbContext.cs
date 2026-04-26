using CANTEEN_SYSTEM.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CANTEEN_SYSTEM.Data;

public class CanteenDbContext(DbContextOptions<CanteenDbContext> options) : DbContext(options)
{
    // These sets become the main tables used by SQLite locally or Azure SQL in production.
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<SyncQueueEntry> SyncQueue => Set<SyncQueueEntry>();
    public DbSet<AppStateEntry> AppState => Set<AppStateEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Keep field sizes and decimal precision explicit so both database providers
        // store the same business data shape.
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(item => item.SyncId).HasMaxLength(32);
            entity.Property(item => item.LastModifiedAt);
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Category).HasMaxLength(80);
            entity.Property(item => item.ImageUrl).HasMaxLength(512);
            entity.Property(item => item.Price).HasPrecision(10, 2);
            entity.HasIndex(item => item.SyncId).IsUnique();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(item => item.SyncId).HasMaxLength(32);
            entity.Property(item => item.LastModifiedAt);
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.QrCode).HasMaxLength(40);
            entity.Property(item => item.Pin).HasMaxLength(12);
            entity.Property(item => item.Role).HasMaxLength(20);
            entity.HasIndex(item => item.QrCode).IsUnique();
            entity.HasIndex(item => item.SyncId).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(item => item.SyncId).HasMaxLength(32);
            entity.Property(item => item.LastModifiedAt);
            entity.Property(item => item.OrderNumber).HasMaxLength(40);
            entity.Property(item => item.PaymentMethod).HasMaxLength(20);
            entity.Property(item => item.Status).HasMaxLength(20);
            entity.Property(item => item.ReferenceNumber).HasMaxLength(100);
            entity.Property(item => item.TotalAmount).HasPrecision(10, 2);
            entity.Property(item => item.AmountReceived).HasPrecision(10, 2);
            entity.Property(item => item.Change).HasPrecision(10, 2);
            entity.HasIndex(item => item.SyncId).IsUnique();
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(item => item.SyncId).HasMaxLength(32);
            entity.Property(item => item.LastModifiedAt);
            entity.Property(item => item.ProductName).HasMaxLength(160);
            entity.Property(item => item.UnitPrice).HasPrecision(10, 2);
            entity.HasIndex(item => item.SyncId).IsUnique();
            // Order items should be removed automatically when the parent order is deleted.
            entity.HasOne(item => item.Order)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncQueueEntry>(entity =>
        {
            entity.Property(item => item.EntityType).HasMaxLength(40);
            entity.Property(item => item.EntitySyncId).HasMaxLength(32);
            entity.Property(item => item.Operation).HasMaxLength(20);
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.EntityType, item.EntitySyncId, item.Operation });
        });

        modelBuilder.Entity<AppStateEntry>(entity =>
        {
            entity.HasKey(item => item.Key);
            entity.Property(item => item.Key).HasMaxLength(120);
            entity.Property(item => item.Value).HasMaxLength(4000);
        });
    }
}
