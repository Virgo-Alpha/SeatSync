namespace SeatSync.Domain.Entities;

public sealed class Event
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = default!;
    public DateTimeOffset StartsAt { get; private set; }

    // Navigation
    private readonly List<Seat> _seats = new();
    public IReadOnlyCollection<Seat> Seats => _seats.AsReadOnly();

    private Event() { } // EF Core needs this

    public Event(string name, DateTimeOffset startsAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event name is required.", nameof(name));

        Name = name.Trim();
        StartsAt = startsAt;
    }

    public void AddSeat(Seat seat)
    {
        _seats.Add(seat);
    }
}