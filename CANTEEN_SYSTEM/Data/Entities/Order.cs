namespace CANTEEN_SYSTEM.Data.Entities;

public class Order
{
    public int Id { get; set; }
    public string? SyncId { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ReferenceNumber { get; set; }
    public decimal? AmountReceived { get; set; }
    public decimal? Change { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
