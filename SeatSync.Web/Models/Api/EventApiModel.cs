namespace SeatSync.Web.Models.Api;

public sealed record EventApiModel(
    Guid Id,
    string Name,
    DateTimeOffset StartsAt);
