using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatSync.Api.Auth;
using SeatSync.Infrastructure.Services;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/holds")]
[Authorize]
public sealed class HoldsController : ControllerBase
{
    private readonly IReservationStoredProcedureService _reservationService;

    public HoldsController(IReservationStoredProcedureService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateHold(
        [FromBody] CreateHoldRequest request,
        CancellationToken ct)
    {
        var holdResult = await _reservationService.CreateSeatHoldAsync(
            request.EventId,
            User.GetRequiredUserId(),
            request.SeatIds,
            TimeSpan.FromMinutes(10),
            ct);

        return holdResult.ResultCode switch
        {
            ReservationResultCode.Success => Ok(new
            {
                holdResult.HoldId,
                holdResult.ExpiresAt
            }),
            ReservationResultCode.NotFound => NotFound(holdResult.Message),
            ReservationResultCode.Conflict => Conflict(holdResult.Message),
            ReservationResultCode.Forbidden => Forbid(),
            _ => BadRequest(holdResult.Message)
        };
    }

    [HttpPost("release-expired")]
    public async Task<ActionResult<object>> ReleaseExpiredHolds(CancellationToken ct)
    {
        var result = await _reservationService.ReleaseExpiredHoldsAsync(ct);
        return Ok(new { result.ReleasedCount });
    }
}

public record CreateHoldRequest(
    Guid EventId,
    List<Guid> SeatIds);
