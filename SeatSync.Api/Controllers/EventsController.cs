using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Api.Auth;
using SeatSync.Api.Contracts.Events;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly SeatSyncDbContext _db;

    public EventsController(SeatSyncDbContext db) => _db = db;
    
    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="request">Event creation payload.</param>
    /// <returns>The created event.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<ActionResult<EventResponse>> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var ev = new Event(request.Name, request.StartsAt, request.Agenda, userId);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Events.Add(ev);

        if (request.CopySeatsFromEventId.HasValue)
        {
            var copied = await CloneSeatInventoryAsync(
                sourceEventId: request.CopySeatsFromEventId.Value,
                targetEventId: ev.Id,
                ct);

            if (!copied.Success)
            {
                await tx.RollbackAsync(ct);
                return copied.ErrorStatusCode switch
                {
                    404 => NotFound(copied.ErrorMessage),
                    _ => BadRequest(copied.ErrorMessage)
                };
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return CreatedAtAction(nameof(GetById), new { eventId = ev.Id },
            new EventResponse(ev.Id, ev.Name, ev.StartsAt, ev.Agenda));
    }

    [HttpPost("{eventId:guid}/seat-inventory/generate")]
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<ActionResult<object>> GenerateSeatInventory(
        Guid eventId,
        [FromBody] GenerateSeatInventoryRequest request,
        CancellationToken ct)
    {
        var targetExists = await _db.Events.AnyAsync(e => e.Id == eventId, ct);
        if (!targetExists) return NotFound("Target event not found.");

        var sourceExists = await _db.Events.AnyAsync(e => e.Id == request.SourceEventId, ct);
        if (!sourceExists) return NotFound("Source event not found.");

        var alreadyGenerated = await _db.Seats.AnyAsync(s => s.EventId == eventId, ct);
        if (alreadyGenerated)
        {
            return Ok(new
            {
                Generated = false,
                CopiedSeatCount = 0,
                Message = "Seat inventory already exists for this event."
            });
        }

        var cloned = await CloneSeatInventoryAsync(request.SourceEventId, eventId, ct);
        if (!cloned.Success)
        {
            return cloned.ErrorStatusCode switch
            {
                404 => NotFound(cloned.ErrorMessage),
                _ => BadRequest(cloned.ErrorMessage)
            };
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new
        {
            Generated = true,
            CopiedSeatCount = cloned.CopiedSeatCount,
            Message = "Seat inventory generated."
        });
    }

    [HttpGet("{eventId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventResponse>> GetById(Guid eventId, CancellationToken ct)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new EventResponse(x.Id, x.Name, x.StartsAt, x.Agenda))
            .SingleOrDefaultAsync(ct);

        if (ev is null) return NotFound();
        return Ok(ev);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<EventResponse>>> GetAll(CancellationToken ct)
    {
        var events = await _db.Events
            .AsNoTracking()
            .OrderBy(x => x.StartsAt)
            .Select(x => new EventResponse(x.Id, x.Name, x.StartsAt, x.Agenda))
            .ToListAsync(ct);

        return Ok(events);
    }

    [HttpPut("{eventId:guid}")]
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<ActionResult<EventResponse>> Update(
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        CancellationToken ct)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (ev is null)
        {
            return NotFound("Event not found.");
        }

        ev.UpdateDetails(request.Name, request.StartsAt, request.Agenda);
        await _db.SaveChangesAsync(ct);

        return Ok(new EventResponse(ev.Id, ev.Name, ev.StartsAt, ev.Agenda));
    }

    [HttpDelete("{eventId:guid}")]
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<ActionResult<object>> Delete(Guid eventId, CancellationToken ct)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (ev is null)
        {
            return NotFound("Event not found.");
        }

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync(ct);
        return Ok(new { Message = "Event deleted." });
    }

    [HttpGet("{eventId:guid}/reservations")]
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<ActionResult<IReadOnlyList<EventReservationResponse>>> GetReservations(
        Guid eventId,
        CancellationToken ct)
    {
        var eventExists = await _db.Events.AnyAsync(x => x.Id == eventId, ct);
        if (!eventExists)
        {
            return NotFound("Event not found.");
        }

        var seatLabelsByOrder = await _db.SeatStatuses
            .Where(x => x.EventId == eventId && x.OrderId != null)
            .Join(_db.Seats,
                status => status.SeatId,
                seat => seat.Id,
                (status, seat) => new
                {
                    OrderId = status.OrderId!.Value,
                    SeatLabel = $"{seat.Row}{seat.Number}"
                })
            .ToListAsync(ct);

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Join(_db.AppUsers,
                order => order.UserId,
                user => user.Id,
                (order, user) => new
                {
                    order.Id,
                    order.State,
                    order.TotalAmount,
                    order.Currency,
                    order.CreatedAt,
                    order.CompletedAt,
                    BookerName = user.DisplayName,
                    BookerEmail = user.Email
                })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var response = orders
            .Select(order => new EventReservationResponse(
                order.Id,
                order.BookerName,
                order.BookerEmail,
                seatLabelsByOrder
                    .Where(x => x.OrderId == order.Id)
                    .Select(x => x.SeatLabel)
                    .OrderBy(x => x)
                    .ToList(),
                order.State.ToString(),
                order.TotalAmount,
                order.Currency,
                order.CreatedAt,
                order.CompletedAt))
            .ToList();

        return Ok(response);
    }

    private async Task<(bool Success, int CopiedSeatCount, int? ErrorStatusCode, string? ErrorMessage)> CloneSeatInventoryAsync(
        Guid sourceEventId,
        Guid targetEventId,
        CancellationToken ct)
    {
        if (sourceEventId == targetEventId)
        {
            return (false, 0, 400, "Source and target event must be different.");
        }

        var sourceSeats = await _db.Seats
            .AsNoTracking()
            .Where(s => s.EventId == sourceEventId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Number)
            .ToListAsync(ct);

        if (sourceSeats.Count == 0)
        {
            return (false, 0, 400, "Source event has no seats to copy.");
        }

        foreach (var sourceSeat in sourceSeats)
        {
            var clonedSeat = new Seat(
                targetEventId,
                sourceSeat.Section,
                sourceSeat.Row,
                sourceSeat.Number,
                sourceSeat.X,
                sourceSeat.Y);

            _db.Seats.Add(clonedSeat);
            _db.SeatStatuses.Add(new SeatStatus(targetEventId, clonedSeat.Id, SeatState.Available));
        }

        return (true, sourceSeats.Count, null, null);
    }
}

public sealed record GenerateSeatInventoryRequest(Guid SourceEventId);
public sealed record EventReservationResponse(
    Guid OrderId,
    string BookerName,
    string BookerEmail,
    IReadOnlyList<string> Seats,
    string PaymentState,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
