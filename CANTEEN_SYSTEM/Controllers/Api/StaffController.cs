using CANTEEN_SYSTEM.Contracts;
using CANTEEN_SYSTEM.Data;
using CANTEEN_SYSTEM.Data.Entities;
using CANTEEN_SYSTEM.Extensions;
using CANTEEN_SYSTEM.Services.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CANTEEN_SYSTEM.Controllers.Api;

[ApiController]
[Route("api/staff")]
public class StaffController(CanteenDbContext db, SyncQueueService syncQueue) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<EmployeeDto>> Login([FromBody] LoginRequest request)
    {
        // Login stays database-backed so the same staff accounts work across devices.
        var employee = await db.Employees.FirstOrDefaultAsync(item =>
            item.QrCode == request.QrCode.Trim().ToUpper() &&
            item.Pin == request.Pin.Trim());

        return employee is null
            ? Unauthorized(new { message = "Invalid QR code or PIN." })
            : Ok(employee.ToDto());
    }

    [HttpGet("employees")]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetEmployees()
    {
        var employees = await db.Employees
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();

        return Ok(employees.Select(item => item.ToDto()).ToList());
    }

    [HttpPost("employees")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee([FromBody] CreateEmployeeRequest request)
    {
        var actor = await db.Employees.FirstOrDefaultAsync(item => item.Id == request.ActorEmployeeId);
        if (actor is null || !string.Equals(actor.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only admins can manage employees." });
        }

        var pin = request.Pin.Trim();
        var name = request.Name.Trim();
        var role = request.Role.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(pin))
        {
            return BadRequest(new { message = "Name and PIN are required." });
        }

        if (pin.Length < 4 || pin.Length > 6 || !pin.All(char.IsDigit))
        {
            return BadRequest(new { message = "PIN must be 4 to 6 digits." });
        }

        if (role is not ("admin" or "cashier"))
        {
            return BadRequest(new { message = "Role must be admin or cashier." });
        }

        var employee = new Employee
        {
            SyncId = Guid.NewGuid().ToString("N"),
            Name = name,
            Pin = pin,
            Role = role,
            QrCode = $"EMP{DateTime.UtcNow.Ticks.ToString()[^6..]}",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };

        db.Employees.Add(employee);
        await syncQueue.QueueUpsertAsync(db, "employee", employee.SyncId!, employee.LastModifiedAt!.Value);
        await db.SaveChangesAsync();

        return Created(string.Empty, employee.ToDto());
    }

    [HttpDelete("employees/{id:int}")]
    public async Task<IActionResult> DeleteEmployee(int id, [FromQuery] int actorEmployeeId)
    {
        var actor = await db.Employees.FirstOrDefaultAsync(item => item.Id == actorEmployeeId);
        if (actor is null || !string.Equals(actor.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only admins can manage employees." });
        }

        if (actor.Id == id)
        {
            return BadRequest(new { message = "You cannot delete the currently logged in admin." });
        }

        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        employee.SyncId ??= Guid.NewGuid().ToString("N");
        await syncQueue.QueueDeleteAsync(db, "employee", employee.SyncId);
        db.Employees.Remove(employee);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
