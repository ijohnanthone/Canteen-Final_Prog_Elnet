namespace CANTEEN_SYSTEM.Data.Entities;

public class Employee
{
    public int Id { get; set; }
    public string? SyncId { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string Role { get; set; } = "cashier";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
