using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatSync.Api.Auth;
using SeatSync.Api.Utilities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;
using SeatSync.Infrastructure.Services;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IReservationStoredProcedureService _reservationService;
    private readonly SeatSyncDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IReservationStoredProcedureService reservationService,
        SeatSyncDbContext db,
        TimeProvider clock,
        ILogger<OrdersController> logger)
    {
        _reservationService = reservationService;
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    [HttpPost("finalize")]
    public async Task<ActionResult<object>> FinalizeOrder(
        [FromBody] FinalizeOrderRequest request,
        CancellationToken ct)
    {
        var orderResult = await _reservationService.FinalizeOrderAsync(
            request.HoldId,
            User.GetRequiredUserId(),
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

    [HttpPost("{orderId:guid}/payments/mock")]
    public async Task<ActionResult<object>> MockCapturePayment(
        Guid orderId,
        [FromBody] MockPaymentRequest request,
        CancellationToken ct)
    {
        var order = await _db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null)
        {
            return NotFound("Order not found.");
        }

        if (!User.IsAdminOrOrganizer() && order.UserId != User.GetRequiredUserId())
        {
            return Forbid();
        }

        if (order.State == PaymentState.Captured)
        {
            return Ok(new { order.Id, State = order.State.ToString(), Message = "Payment already captured." });
        }

        if (order.State == PaymentState.Pending)
        {
            order.MarkAuthorized();
        }

        if (request.ShouldSucceed)
        {
            order.MarkCaptured(_clock);
        }
        else
        {
            order.MarkFailed();
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            order.Id,
            State = order.State.ToString(),
            order.CompletedAt,
            Message = request.ShouldSucceed ? "Mock payment captured." : "Mock payment failed."
        });
    }

    [AllowAnonymous]
    [HttpGet("{orderId:guid}/receipt")]
    public async Task<IActionResult> DownloadReceipt(Guid orderId, CancellationToken ct)
    {
        var receipt = await BuildReceiptAsync(orderId, ct);
        if (receipt is null)
        {
            return NotFound("Order not found.");
        }

        var bytes = Encoding.UTF8.GetBytes(receipt.Content);
        var fileName = $"SeatSync-Receipt-{orderId}.txt";
        return File(bytes, "text/plain", fileName);
    }

    [AllowAnonymous]
    [HttpGet("{orderId:guid}/receipt/pdf")]
    public async Task<IActionResult> DownloadReceiptPdf(Guid orderId, CancellationToken ct)
    {
        var receipt = await BuildReceiptAsync(orderId, ct);
        if (receipt is null)
        {
            return NotFound("Order not found.");
        }

        var pdfBytes = SimplePdfBuilder.BuildSinglePageReceipt(receipt.Lines);
        var fileName = $"SeatSync-Receipt-{orderId}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpPost("{orderId:guid}/receipt/email")]
    public async Task<ActionResult<object>> EmailReceipt(
        Guid orderId,
        [FromBody] EmailReceiptRequest request,
        CancellationToken ct)
    {
        var receipt = await BuildReceiptAsync(orderId, ct);
        if (receipt is null)
        {
            return NotFound("Order not found.");
        }

        if (!User.IsAdminOrOrganizer() && receipt.UserId != User.GetRequiredUserId())
        {
            return Forbid();
        }

        var recipient = string.IsNullOrWhiteSpace(request.EmailTo)
            ? receipt.UserEmail
            : request.EmailTo.Trim();

        _logger.LogInformation(
            "Mock receipt email queued. OrderId: {OrderId}, To: {Recipient}, Subject: SeatSync receipt for {EventName}",
            orderId,
            recipient,
            receipt.EventName);

        return Ok(new
        {
            orderId,
            recipient,
            message = "Receipt email queued (mock)."
        });
    }

    private async Task<ReceiptModel?> BuildReceiptAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null)
        {
            return null;
        }

        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.EventId, ct);
        var user = await _db.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.UserId, ct);

        var seatRows = await _db.SeatStatuses
            .Where(x => x.OrderId == orderId)
            .Join(_db.Seats,
                status => status.SeatId,
                seat => seat.Id,
                (status, seat) => new { seat.Row, seat.Number })
            .ToListAsync(ct);

        var seats = seatRows
            .Select(x => $"{x.Row}{x.Number}")
            .OrderBy(x => x)
            .ToList();

        var seatText = seats.Count == 0 ? "N/A" : string.Join(", ", seats);
        var lines = new List<string>
        {
            "SeatSync Receipt",
            "---------------",
            $"Order Id: {order.Id}",
            $"Event: {ev?.Name ?? "Unknown Event"}",
            $"Event Date/Time: {ev?.StartsAt:yyyy-MM-dd HH:mm} UTC",
            $"Booker: {user?.DisplayName ?? "Unknown User"}",
            $"Booker Email: {user?.Email ?? "unknown@example.com"}",
            $"Seats: {seatText}",
            $"Amount: {order.TotalAmount:0.00} {order.Currency}",
            $"Payment State: {order.State}",
            $"Completed At: {(order.CompletedAt.HasValue ? order.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") : "Not completed")}",
            $"Issued At: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss 'UTC'}"
        };

        var receiptContent = string.Join(Environment.NewLine, lines);

        return new ReceiptModel(
            order.UserId,
            user?.Email ?? "unknown@example.com",
            ev?.Name ?? "Unknown Event",
            receiptContent,
            lines);
    }

    private sealed record ReceiptModel(
        Guid UserId,
        string UserEmail,
        string EventName,
        string Content,
        IReadOnlyList<string> Lines);
}

public record FinalizeOrderRequest(Guid HoldId, string IdempotencyKey);

public record MockPaymentRequest(bool ShouldSucceed = true);

public record EmailReceiptRequest(string? EmailTo);
