namespace SeatSync.Web.Models.Api;

public sealed record UpdateEventRequestApiModel(
    string Name,
    DateTimeOffset StartsAt,
    string? Agenda);
