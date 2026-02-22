using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly SeatSyncDbContext _db;
    private readonly TimeProvider _clock;

    public OrdersController(SeatSyncDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpPost("finalize")]
    public async Task<ActionResult<object>> FinalizeOrder(
        [FromBody] FinalizeOrderRequest request,
        CancellationToken ct)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var hold = await _db.Holds
            .FirstOrDefaultAsync(h => h.Id == request.HoldId, ct);

        if (hold is null)
            return NotFound("Hold not found.");

        if (hold.IsExpired(_clock))
            return BadRequest("Hold expired.");

        if (hold.UserId != request.UserId)
            return Forbid();

        var seats = await _db.SeatStatuses
            .Where(s => s.HoldId == hold.Id)
            .ToListAsync(ct);
        
        var seatIds = seats
            .Select(s => s.SeatId)
            .ToList();
        
        var jwtId = Guid.NewGuid().ToString();

        var order = new Order(
            hold.EventId,
            hold.UserId,
            seats.Count * 100m, // fake price
            "USD",                 // currency (temporary hardcoded)
            request.IdempotencyKey,
            seatIds
            );

        _db.Orders.Add(order);

        foreach (var seat in seats)
        {
            seat.MarkSold(order.Id);

            var ticket = new Ticket(
                order.Id,
                seat.SeatId,
                hold.EventId,
                jwtId
                );

            _db.Tickets.Add(ticket);
        }

        hold.MarkConverted();

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { order.Id });
    }
}

public record FinalizeOrderRequest(
    Guid HoldId,
    Guid UserId,
    string IdempotencyKey);