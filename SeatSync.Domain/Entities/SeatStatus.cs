using SeatSync.Domain.Enums;
namespace SeatSync.Domain.Entities;

public sealed class SeatStatus
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EventId { get; private set; }
    public Guid SeatId { get; private set; }
    public SeatState State { get; private set; }
    public Guid? HoldId { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    
    /// <summary>
    /// 
    /// Why the RowVersion is critical:
    // In a high-traffic ticketing system, two people might try to click the same seat at the exact same millisecond.
    //     Person A reads the seat as Available.
    //     Person B reads the seat as Available.
    //     Person A saves the seat as Held.
    //     Without RowVersion, Person B would also save the seat as Held, overwriting Person A.
    //     With RowVersion, the database will block Person B's save because the version changed when Person A saved their change.
    
    /// </summary>
    // The current schema stores RowVersion as a required varbinary column.
    // Initialize with an empty payload so inserts never send NULL during startup seeding.
    public byte[] RowVersion { get; private set; } = [];

    public bool IsAvailable() => State == SeatState.Available;

    public bool IsHeldBy(Guid holdId) => State == SeatState.Held && HoldId == holdId;
    
    public SeatStatus(Guid eventId, Guid seatId, SeatState state)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        SeatId = seatId;
        State = state;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkHeld(Guid holdId)
    {
        if (State != SeatState.Available)
            throw new InvalidOperationException("Seat is not available for holding.");

        State = SeatState.Held;
        HoldId = holdId;
        OrderId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSold(Guid orderId)
    {
        if (State != SeatState.Held)
            throw new InvalidOperationException("Seat must be held before it can be sold.");

        State = SeatState.Sold;
        OrderId = orderId;
        HoldId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Release()
    {
        if (State != SeatState.Held)
            throw new InvalidOperationException("Only held seats can be released.");

        State = SeatState.Available;
        HoldId = null;
        OrderId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
}
