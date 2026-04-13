namespace SeatSync.Web.Models.Api;

public sealed record CreateEventRequestApiModel(
    string Name,
    DateTimeOffset StartsAt,
    string? Agenda,
    Guid? CopySeatsFromEventId = null
);
