namespace SeatSync.Web.Models.Api;

public sealed record EventReservationApiModel(
    Guid OrderId,
    string BookerName,
    string BookerEmail,
    IReadOnlyList<string> Seats,
    string PaymentState,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
