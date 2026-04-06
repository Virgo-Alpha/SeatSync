using Microsoft.AspNetCore.Mvc;
using SeatSync.Infrastructure.Services;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IReservationStoredProcedureService _reservationService;

    public OrdersController(IReservationStoredProcedureService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost("finalize")]
    public async Task<ActionResult<object>> FinalizeOrder(
        [FromBody] FinalizeOrderRequest request,
        CancellationToken ct)
    {
        var orderResult = await _reservationService.FinalizeOrderAsync(
            request.HoldId,
            request.UserId,
            request.IdempotencyKey,
            ct);

        return orderResult.ResultCode switch
        {
            ReservationResultCode.Success => Ok(new { orderResult.OrderId }),
            ReservationResultCode.NotFound => NotFound(orderResult.Message),
            ReservationResultCode.Forbidden => Forbid(),
            ReservationResultCode.Conflict => Conflict(orderResult.Message),
            _ => BadRequest(orderResult.Message)
        };
    }
}

public record FinalizeOrderRequest(
    Guid HoldId,
    Guid UserId,
    string IdempotencyKey);
