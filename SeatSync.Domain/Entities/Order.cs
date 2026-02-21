namespace SeatSync.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; private set; }
    public PaymentState State { get; private set; }
    
    // Must be unique in the DB to prevent double-charging
    public string IdempotencyKey { get; private set; } 

    public DateTimeOffset CreatedAt { get; private set; }

    // The '?' makes it nullable (null if the order isn't finished)
    public DateTimeOffset? CompletedAt { get; private set; }
    
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal TotalAmount { get; private set; } // Precision is key for money
    public string Currency { get; private set; } = "USD";

    private readonly List<Guid> _seatIds = new();
    public IReadOnlyCollection<Guid> SeatIds => _seatIds.AsReadOnly();

    public enum PaymentState
    {
        Pending,
        Authorized,
        Captured,
        Failed,
        Refunded
    }
    
    public Order(
        Guid eventId, 
        Guid userId, 
        decimal totalAmount, 
        string currency, 
        string idempotencyKey, 
        List<Guid> seatIds)
    {
        if (totalAmount <= 0) throw new ArgumentException("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key required.");
        if (seatIds == null || !seatIds.Any()) throw new ArgumentException("Order must have seats.");

        Id = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        TotalAmount = totalAmount;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        State = PaymentState.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    
        _seatIds.AddRange(seatIds);
    }

    private Order() { } // EF Core
    
    public void MarkAuthorized()
    {
        EnsureNotFinalized();
        if (State != PaymentState.Pending)
            throw new InvalidOperationException("Can only authorize pending orders.");

        State = PaymentState.Authorized;
    }

    public void MarkCaptured(TimeProvider clock)
    {
        if (State != PaymentState.Authorized)
            throw new InvalidOperationException("Can only capture authorized payments.");

        State = PaymentState.Captured;
        CompletedAt = clock.GetUtcNow();
    }

    public void MarkFailed()
    {
        EnsureNotFinalized();
        State = PaymentState.Failed;
    }

    public void MarkRefunded()
    {
        if (State != PaymentState.Captured)
            throw new InvalidOperationException("Cannot refund an order that hasn't been captured.");

        State = PaymentState.Refunded;
    }

    public void EnsureNotFinalized()
    {
        // Once Captured or Refunded, the financial record should not change
        if (State == PaymentState.Captured || State == PaymentState.Refunded)
            throw new InvalidOperationException("Order is in a finalized state and cannot be modified.");
    }

}