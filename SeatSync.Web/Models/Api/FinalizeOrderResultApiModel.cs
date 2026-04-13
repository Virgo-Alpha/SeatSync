namespace SeatSync.Web.Models.Api;

public sealed record FinalizeOrderResultApiModel(
    bool IsSuccess,
    bool IsConflict,
    Guid? OrderId,
    string? Message);
