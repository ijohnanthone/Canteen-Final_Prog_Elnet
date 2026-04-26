namespace CANTEEN_SYSTEM.Data.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public string? SyncId { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
