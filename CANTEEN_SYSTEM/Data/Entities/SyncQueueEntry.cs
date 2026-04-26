namespace CANTEEN_SYSTEM.Data.Entities;

public class SyncQueueEntry
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntitySyncId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
}
