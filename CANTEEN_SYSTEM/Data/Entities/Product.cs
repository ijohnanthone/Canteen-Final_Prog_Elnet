namespace CANTEEN_SYSTEM.Data.Entities;

public class Product
{
    public int Id { get; set; }
    public string? SyncId { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}
