namespace SeatSync.Api.Contracts.Events;

public sealed record UpdateEventRequest(
    string Name,
    DateTimeOffset StartsAt,
    string? Agenda = null);
