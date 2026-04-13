namespace SeatSync.Domain.Entities;

/// <summary>
/// Represents a ticketed event in the system.
/// </summary>
public sealed class Event
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = default!;
    public DateTimeOffset StartsAt { get; private set; }
    public string? Agenda { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    // Navigation
    private readonly List<Seat> _seats = new();
    public IReadOnlyCollection<Seat> Seats => _seats.AsReadOnly();

    private Event() { } // EF Core needs this

    public Event(string name, DateTimeOffset startsAt)
        : this(name, startsAt, null, null)
    {
    }

    public Event(string name, DateTimeOffset startsAt, string? agenda, Guid? createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event name is required.", nameof(name));

        Name = name.Trim();
        StartsAt = startsAt;
        Agenda = string.IsNullOrWhiteSpace(agenda) ? null : agenda.Trim();
        CreatedByUserId = createdByUserId;
    }

    public void AddSeat(Seat seat)
    {
        _seats.Add(seat);
    }
}
