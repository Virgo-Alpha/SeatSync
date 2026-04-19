namespace SeatSync.Web.Models;

public sealed record PendingCheckoutModel(
    Guid HoldId,
    Guid EventId,
    string EventName,
    DateTimeOffset EventStartsAt,
    IReadOnlyList<string> SeatLabels,
    DateTimeOffset HoldExpiresAtUtc);
