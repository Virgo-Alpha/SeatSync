namespace SeatSync.Api.Contracts.Events;

public sealed record CreateEventRequest(
    string Name,
    DateTimeOffset StartsAt,
    string? Agenda = null,
    Guid? CopySeatsFromEventId = null
);
