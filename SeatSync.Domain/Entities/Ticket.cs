namespace SeatSync.Domain.Entities;

public sealed class Ticket
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid SeatId { get; private set; }
    public Guid EventId { get; private set; }
    
    // Unique JTI (JSON Token Identifier) to prevent ticket duplication/cloning
    public string JwtId { get; private set; } = default!;
    
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset? RedeemedAt { get; private set; }

    private Ticket() { }

    public Ticket(Guid orderId, Guid seatId, Guid eventId, string jwtId)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId required");
        if (seatId == Guid.Empty) throw new ArgumentException("SeatId required");
        if (eventId == Guid.Empty) throw new ArgumentException("EventId required");
        if (string.IsNullOrWhiteSpace(jwtId)) throw new ArgumentException("JwtId required");

        Id = Guid.NewGuid();
        OrderId = orderId;
        SeatId = seatId;
        EventId = eventId;
        JwtId = jwtId;
        IssuedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Logical check to see if the ticket has been scanned at the gate.
    /// </summary>
    public bool IsRedeemed() => RedeemedAt.HasValue;

    /// <summary>
    /// Marks the ticket as used. This is a terminal action.
    /// </summary>
    /// <param name="timestamp">The time of entry/scan.</param>
    public void Redeem(DateTimeOffset timestamp)
    {
        if (IsRedeemed())
        {
            throw new InvalidOperationException($"Ticket {Id} was already redeemed at {RedeemedAt}");
        }

        RedeemedAt = timestamp;
    }

}