using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/seats")]
public sealed class SeatsController : ControllerBase
{
    private readonly SeatSyncDbContext _db;

    public SeatsController(SeatSyncDbContext db)
    {
        _db = db;
    }

    // GET /api/events/{eventId}/seats
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetSeats(
        Guid eventId,
        CancellationToken ct)
    {
        var seats = await _db.Seats
            .Where(s => s.EventId == eventId)
            .Join(_db.SeatStatuses,
                seat => seat.Id,
                status => status.SeatId,
                (seat, status) => new
                {
                    seat.Id,
                    seat.Section,
                    seat.Row,
                    seat.Number,
                    seat.X,
                    seat.Y,
                    status.State
                })
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(seats);
    }

    // POST /api/events/{eventId}/seats
    [HttpPost]
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<IActionResult> BulkCreate(
        Guid eventId,
        [FromBody] List<SeatCreateRequest> request,
        CancellationToken ct)
    {
        var exists = await _db.Events.AnyAsync(e => e.Id == eventId, ct);
        if (!exists) return NotFound("Event not found.");

        foreach (var item in request)
        {
            var seat = new Seat(
                eventId,
                item.Section,
                item.Row,
                item.Number,
                item.X,
                item.Y);

            _db.Seats.Add(seat);

            _db.SeatStatuses.Add(new SeatStatus(
                eventId,
                seat.Id,
                SeatState.Available));
        }

        await _db.SaveChangesAsync(ct);

        return Ok();
    }
}

public record SeatCreateRequest(
    string Section,
    string Row,
    string Number,
    decimal X,
    decimal Y);
