using CANTEEN_SYSTEM.Contracts;
using CANTEEN_SYSTEM.Data;
using CANTEEN_SYSTEM.Data.Entities;
using CANTEEN_SYSTEM.Extensions;
using CANTEEN_SYSTEM.Services.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CANTEEN_SYSTEM.Controllers.Api;

[ApiController]
[Route("api/orders")]
public class OrdersController(CanteenDbContext db, SyncQueueService syncQueue) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrders()
    {
        var orders = await db.Orders
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(order => order.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await db.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id);

        return order is null ? NotFound() : Ok(order.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Order must contain at least one item." });
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new { message = "One or more products were not found." });
        }

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
            {
                return BadRequest(new { message = "Quantity must be greater than zero." });
            }

            var product = products[line.ProductId];
            if (product.Stock < line.Quantity)
            {
                return BadRequest(new { message = $"{product.Name} does not have enough stock." });
            }
        }

        var totalAmount = request.Items.Sum(line => products[line.ProductId].Price * line.Quantity);
        var changedAt = DateTime.UtcNow;
        var orderSyncId = Guid.NewGuid().ToString("N");
        var order = new Order
        {
            SyncId = orderSyncId,
            OrderNumber = $"ORD{DateTime.UtcNow:yyMMddHHmmssfff}",
            PaymentMethod = request.PaymentMethod.Trim().ToLowerInvariant(),
            Status = request.PaymentMethod.Equals("cash", StringComparison.OrdinalIgnoreCase) ? "paid" : "pending",
            CreatedAt = changedAt,
            LastModifiedAt = changedAt,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            AmountReceived = request.AmountReceived,
            Change = request.AmountReceived.HasValue ? request.AmountReceived.Value - totalAmount : null,
            TotalAmount = totalAmount
        };

        foreach (var line in request.Items)
        {
            var product = products[line.ProductId];
            product.SyncId ??= Guid.NewGuid().ToString("N");
            // Reduce inventory at the same time the order is stored so stock stays in sync.
            product.Stock -= line.Quantity;
            product.LastModifiedAt = changedAt;
            order.Items.Add(new OrderItem
            {
                SyncId = Guid.NewGuid().ToString("N"),
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = line.Quantity,
                UnitPrice = product.Price,
                LastModifiedAt = changedAt
            });
        }

        db.Orders.Add(order);
        foreach (var product in products.Values)
        {
            await syncQueue.QueueUpsertAsync(db, "product", product.SyncId!, changedAt);
        }

        await syncQueue.QueueUpsertAsync(db, "order", order.SyncId!, changedAt);
        await db.SaveChangesAsync();

        await db.Entry(order).Collection(item => item.Items).LoadAsync();

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order.ToDto());
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var actor = await db.Employees.FirstOrDefaultAsync(item => item.Id == request.ActorEmployeeId);
        if (actor is null)
        {
            return Unauthorized(new { message = "Employee session is not valid." });
        }

        var order = await db.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        var nextStatus = request.Status.Trim().ToLowerInvariant();
        var allowedStatuses = new[] { "pending", "paid", "preparing", "completed", "cancelled" };
        if (!allowedStatuses.Contains(nextStatus))
        {
            return BadRequest(new { message = "Unsupported order status." });
        }

        order.Status = nextStatus;
        order.SyncId ??= Guid.NewGuid().ToString("N");
        order.LastModifiedAt = DateTime.UtcNow;
        await syncQueue.QueueUpsertAsync(db, "order", order.SyncId, order.LastModifiedAt.Value);
        await db.SaveChangesAsync();

        return Ok(order.ToDto());
    }
}
