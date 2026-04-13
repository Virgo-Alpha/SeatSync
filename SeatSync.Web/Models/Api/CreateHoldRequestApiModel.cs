namespace SeatSync.Web.Models.Api;

public sealed record CreateHoldRequestApiModel(
    Guid EventId,
    List<Guid> SeatIds);
