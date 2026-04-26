using CANTEEN_SYSTEM.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CANTEEN_SYSTEM.Services.Sync;

public class CloudSyncWorker(IServiceScopeFactory scopeFactory, IOptions<CloudSyncOptions> options, ILogger<CloudSyncWorker> logger) : BackgroundService
{
    private readonly CloudSyncOptions syncOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, syncOptions.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var localDb = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
                var syncService = scope.ServiceProvider.GetRequiredService<CloudSyncService>();

                await SyncSchemaManager.EnsureAsync(localDb);
                await DbInitializer.EnsureSyncMetadataAsync(localDb);
                await localDb.SaveChangesAsync(stoppingToken);

                var synced = await syncService.SyncPendingChangesAsync(localDb, stoppingToken);
                if (synced > 0)
                {
                    logger.LogInformation("Cloud sync pushed {Count} change(s) to Azure.", synced);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Cloud sync worker will retry later.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
