using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/holds")]
public sealed class HoldsController : ControllerBase
{
    private readonly SeatSyncDbContext _db;

    public HoldsController(SeatSyncDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateHold(
        [FromBody] CreateHoldRequest request,
        CancellationToken ct)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var seatStatuses = await _db.SeatStatuses
            .Where(s => request.SeatIds.Contains(s.SeatId)
                        && s.EventId == request.EventId)
            .ToListAsync(ct);

        if (seatStatuses.Count != request.SeatIds.Count)
            return BadRequest("Some seats not found.");

        if (seatStatuses.Any(s => s.State != SeatState.Available))
            return BadRequest("One or more seats unavailable.");

        var hold = new Hold(
            request.EventId,
            request.UserId,
            request.SeatIds,
            TimeSpan.FromMinutes(10));

        _db.Holds.Add(hold);

        foreach (var seat in seatStatuses)
        {
            seat.MarkHeld(hold.Id);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new
        {
            hold.Id,
            hold.ExpiresAt
        });
    }
}

public record CreateHoldRequest(
    Guid EventId,
    Guid UserId,
    List<Guid> SeatIds);