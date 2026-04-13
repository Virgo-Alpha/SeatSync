namespace SeatSync.Web.Models.Api;

public sealed record FinalizeOrderRequestApiModel(
    Guid HoldId,
    string IdempotencyKey);
