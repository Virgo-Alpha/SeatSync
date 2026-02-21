namespace SeatSync.Domain.Entities;

public sealed class Hold
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public HoldState State { get; private set; } = HoldState.Active;
    
    // Encapsulated collection: prevents outside code from calling .Add()
    private readonly List<Guid> _seatIds = new();
    public IReadOnlyCollection<Guid> SeatIds => _seatIds.AsReadOnly();
    
    public enum HoldState
    {
        Active,
        Expired,
        Converted,
        Cancelled
    }
    
    public Hold(Guid eventId, Guid userId, List<Guid> seatIds, TimeSpan holdDuration)
    {
        if (seatIds == null || !seatIds.Any())
            throw new ArgumentException("Hold must include at least one seat.");

        Id = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt.Add(holdDuration);
        State = HoldState.Active;
    
        _seatIds.AddRange(seatIds);
    }

    // Required for EF Core
    private Hold() { }
    
    // Use TimeProvider (the modern .NET replacement for IClock)
    public bool IsExpired(TimeProvider clock) 
        => clock.GetUtcNow() >= ExpiresAt;

    public void EnsureActive()
    {
        if (State != HoldState.Active)
            throw new InvalidOperationException($"Hold is not active (Current State: {State})");
    }

    public void ValidateOwnership(Guid userId)
    {
        if (UserId != userId)
            throw new UnauthorizedAccessException("User does not own this hold.");
    }

    public void MarkExpired()
    {
        // Expired is a terminal state for logic, though often checked via time
        State = HoldState.Expired;
    }

    public void MarkConverted()
    {
        // Ensures that expired holds cannot be converted
        EnsureActive();
        State = HoldState.Converted;
    }

    public void Cancel()
    {
        if (State == HoldState.Converted)
            throw new InvalidOperationException("Cannot cancel a hold that has already been converted to an order.");
    
        State = HoldState.Cancelled;
    }

}