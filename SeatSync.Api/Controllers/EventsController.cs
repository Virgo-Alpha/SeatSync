using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Api.Contracts.Events;
using SeatSync.Domain.Entities;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly SeatSyncDbContext _db;

    public EventsController(SeatSyncDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<EventResponse>> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var ev = new Event(request.Name, request.StartsAt);
        _db.Events.Add(ev);

        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { eventId = ev.Id },
            new EventResponse(ev.Id, ev.Name, ev.StartsAt));
    }

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<EventResponse>> GetById(Guid eventId, CancellationToken ct)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new EventResponse(x.Id, x.Name, x.StartsAt))
            .SingleOrDefaultAsync(ct);

        if (ev is null) return NotFound();
        return Ok(ev);
    }
}