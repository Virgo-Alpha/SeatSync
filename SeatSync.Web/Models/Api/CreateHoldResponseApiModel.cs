namespace SeatSync.Web.Models.Api;

public sealed record CreateHoldResponseApiModel(
    Guid HoldId,
    DateTimeOffset ExpiresAt);
