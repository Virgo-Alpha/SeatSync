using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController : ControllerBase
{
    private readonly SeatSyncDbContext _db;

    public TicketsController(SeatSyncDbContext db)
    {
        _db = db;
    }

    [HttpPost("redeem")]
    public async Task<ActionResult> Redeem(
        [FromBody] RedeemTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.JwtId == request.JwtId, ct);

        if (ticket is null)
            return NotFound();

        if (ticket.IsRedeemed())
            return BadRequest("Already redeemed.");

        ticket.Redeem(DateTimeOffset.UtcNow);

        await _db.SaveChangesAsync(ct);

        return Ok();
    }
}

public record RedeemTicketRequest(string JwtId);