namespace CANTEEN_SYSTEM.Services.Sync;

public class CloudSyncOptions
{
    public const string SectionName = "CloudSync";

    public string? AzureSqlConnectionString { get; set; }
    public int IntervalSeconds { get; set; } = 15;
}
