using CANTEEN_SYSTEM.Data;
using CANTEEN_SYSTEM.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CANTEEN_SYSTEM.Services.Sync;

public class CloudSyncService(ILogger<CloudSyncService> logger, IOptions<CloudSyncOptions> options)
{
    private readonly CloudSyncOptions syncOptions = options.Value;

    public async Task<int> SyncPendingChangesAsync(CanteenDbContext localDb, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(syncOptions.AzureSqlConnectionString))
        {
            return 0;
        }

        var pendingChanges = await localDb.SyncQueue
            .OrderBy(item => item.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingChanges.Count == 0)
        {
            return 0;
        }

        await using var cloudDb = CreateCloudDbContext(syncOptions.AzureSqlConnectionString);
        await SyncSchemaManager.EnsureAsync(cloudDb);
        await DbInitializer.EnsureSyncMetadataAsync(cloudDb);
        await cloudDb.SaveChangesAsync(cancellationToken);

        var syncedCount = 0;

        foreach (var change in pendingChanges)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                change.LastAttemptAt = DateTime.UtcNow;
                change.LastError = null;

                await ApplyChangeAsync(localDb, cloudDb, change, cancellationToken);
                localDb.SyncQueue.Remove(change);
                syncedCount++;
            }
            catch (Exception ex)
            {
                change.LastError = ex.Message[..Math.Min(ex.Message.Length, 1900)];
                logger.LogWarning(ex, "Cloud sync failed for {EntityType} {EntitySyncId}", change.EntityType, change.EntitySyncId);
                break;
            }
        }

        await localDb.SaveChangesAsync(cancellationToken);
        return syncedCount;
    }

    private static CanteenDbContext CreateCloudDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseAzureSql(connectionString)
            .Options;

        return new CanteenDbContext(options);
    }

    private static async Task ApplyChangeAsync(CanteenDbContext localDb, CanteenDbContext cloudDb, SyncQueueEntry change, CancellationToken cancellationToken)
    {
        switch (change.EntityType)
        {
            case "product":
                await SyncProductAsync(localDb, cloudDb, change, cancellationToken);
                break;
            case "employee":
                await SyncEmployeeAsync(localDb, cloudDb, change, cancellationToken);
                break;
            case "order":
                await SyncOrderAsync(localDb, cloudDb, change, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported sync entity type '{change.EntityType}'.");
        }

        await cloudDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task SyncProductAsync(CanteenDbContext localDb, CanteenDbContext cloudDb, SyncQueueEntry change, CancellationToken cancellationToken)
    {
        if (change.Operation == "delete")
        {
            var existing = await cloudDb.Products.FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken);
            if (existing is not null)
            {
                cloudDb.Products.Remove(existing);
            }

            return;
        }

        var localEntity = await localDb.Products.AsNoTracking()
            .FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken)
            ?? throw new InvalidOperationException("Local product could not be found for sync.");

        var remoteEntity = await cloudDb.Products.FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken);
        if (remoteEntity is null)
        {
            cloudDb.Products.Add(new Product
            {
                SyncId = localEntity.SyncId,
                LastModifiedAt = localEntity.LastModifiedAt,
                Name = localEntity.Name,
                Category = localEntity.Category,
                Price = localEntity.Price,
                Stock = localEntity.Stock,
                ImageUrl = localEntity.ImageUrl
            });
            return;
        }

        remoteEntity.LastModifiedAt = localEntity.LastModifiedAt;
        remoteEntity.Name = localEntity.Name;
        remoteEntity.Category = localEntity.Category;
        remoteEntity.Price = localEntity.Price;
        remoteEntity.Stock = localEntity.Stock;
        remoteEntity.ImageUrl = localEntity.ImageUrl;
    }

    private static async Task SyncEmployeeAsync(CanteenDbContext localDb, CanteenDbContext cloudDb, SyncQueueEntry change, CancellationToken cancellationToken)
    {
        if (change.Operation == "delete")
        {
            var existing = await cloudDb.Employees.FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken);
            if (existing is not null)
            {
                cloudDb.Employees.Remove(existing);
            }

            return;
        }

        var localEntity = await localDb.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken)
            ?? throw new InvalidOperationException("Local employee could not be found for sync.");

        var remoteEntity = await cloudDb.Employees.FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken);
        if (remoteEntity is null)
        {
            cloudDb.Employees.Add(new Employee
            {
                SyncId = localEntity.SyncId,
                LastModifiedAt = localEntity.LastModifiedAt,
                Name = localEntity.Name,
                QrCode = localEntity.QrCode,
                Pin = localEntity.Pin,
                Role = localEntity.Role,
                CreatedAt = localEntity.CreatedAt
            });
            return;
        }

        remoteEntity.LastModifiedAt = localEntity.LastModifiedAt;
        remoteEntity.Name = localEntity.Name;
        remoteEntity.QrCode = localEntity.QrCode;
        remoteEntity.Pin = localEntity.Pin;
        remoteEntity.Role = localEntity.Role;
        remoteEntity.CreatedAt = localEntity.CreatedAt;
    }

    private static async Task SyncOrderAsync(CanteenDbContext localDb, CanteenDbContext cloudDb, SyncQueueEntry change, CancellationToken cancellationToken)
    {
        if (change.Operation == "delete")
        {
            var existing = await cloudDb.Orders
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken);

            if (existing is not null)
            {
                cloudDb.Orders.Remove(existing);
            }

            return;
        }

        var localEntity = await localDb.Orders.AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken)
            ?? throw new InvalidOperationException("Local order could not be found for sync.");

        var remoteEntity = await cloudDb.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.SyncId == change.EntitySyncId, cancellationToken);

        if (remoteEntity is null)
        {
            remoteEntity = new Order
            {
                SyncId = localEntity.SyncId
            };
            cloudDb.Orders.Add(remoteEntity);
        }

        remoteEntity.LastModifiedAt = localEntity.LastModifiedAt;
        remoteEntity.OrderNumber = localEntity.OrderNumber;
        remoteEntity.TotalAmount = localEntity.TotalAmount;
        remoteEntity.PaymentMethod = localEntity.PaymentMethod;
        remoteEntity.Status = localEntity.Status;
        remoteEntity.CreatedAt = localEntity.CreatedAt;
        remoteEntity.ReferenceNumber = localEntity.ReferenceNumber;
        remoteEntity.AmountReceived = localEntity.AmountReceived;
        remoteEntity.Change = localEntity.Change;

        if (remoteEntity.Items.Count > 0)
        {
            cloudDb.OrderItems.RemoveRange(remoteEntity.Items);
        }

        remoteEntity.Items.Clear();
        foreach (var localItem in localEntity.Items.OrderBy(item => item.Id))
        {
            remoteEntity.Items.Add(new OrderItem
            {
                SyncId = localItem.SyncId,
                LastModifiedAt = localItem.LastModifiedAt,
                ProductId = localItem.ProductId,
                ProductName = localItem.ProductName,
                Quantity = localItem.Quantity,
                UnitPrice = localItem.UnitPrice
            });
        }
    }
}
