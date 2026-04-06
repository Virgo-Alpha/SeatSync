using Microsoft.AspNetCore.Mvc;
using SeatSync.Infrastructure.Services;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/holds")]
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
            request.UserId,
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
}

public record CreateHoldRequest(
    Guid EventId,
    Guid UserId,
    List<Guid> SeatIds);
