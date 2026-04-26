using CANTEEN_SYSTEM.Data;
using CANTEEN_SYSTEM.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CANTEEN_SYSTEM.Services.Sync;

public class SyncQueueService
{
    public async Task QueueUpsertAsync(CanteenDbContext db, string entityType, string entitySyncId, DateTime changedAt)
    {
        if (await db.SyncQueue.AnyAsync(item =>
            item.EntityType == entityType &&
            item.EntitySyncId == entitySyncId &&
            item.Operation == "upsert"))
        {
            return;
        }

        db.SyncQueue.Add(new SyncQueueEntry
        {
            EntityType = entityType,
            EntitySyncId = entitySyncId,
            Operation = "upsert",
            CreatedAt = changedAt
        });
    }

    public async Task QueueDeleteAsync(CanteenDbContext db, string entityType, string entitySyncId)
    {
        var staleUpserts = await db.SyncQueue
            .Where(item => item.EntityType == entityType && item.EntitySyncId == entitySyncId)
            .ToListAsync();

        if (staleUpserts.Count > 0)
        {
            db.SyncQueue.RemoveRange(staleUpserts);
        }

        db.SyncQueue.Add(new SyncQueueEntry
        {
            EntityType = entityType,
            EntitySyncId = entitySyncId,
            Operation = "delete",
            CreatedAt = DateTime.UtcNow
        });
    }
}
