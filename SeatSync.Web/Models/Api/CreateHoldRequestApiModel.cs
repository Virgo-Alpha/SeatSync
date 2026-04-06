namespace SeatSync.Web.Models.Api;

public sealed record CreateHoldRequestApiModel(
    Guid EventId,
    Guid UserId,
    List<Guid> SeatIds);
