namespace SeatSync.Api.Contracts.Events;

public sealed record EventResponse(
    Guid Id,
    string Name,
    DateTimeOffset StartsAt
);