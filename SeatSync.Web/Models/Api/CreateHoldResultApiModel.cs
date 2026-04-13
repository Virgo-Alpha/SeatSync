namespace SeatSync.Web.Models.Api;

public sealed record CreateHoldResultApiModel(
    bool IsSuccess,
    bool IsConflict,
    Guid? HoldId,
    DateTimeOffset? ExpiresAt,
    string? Message);
