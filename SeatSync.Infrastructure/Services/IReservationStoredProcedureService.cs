namespace SeatSync.Infrastructure.Services;

public interface IReservationStoredProcedureService
{
    Task<CreateHoldStoredProcedureResult> CreateSeatHoldAsync(
        Guid eventId,
        Guid userId,
        IReadOnlyCollection<Guid> seatIds,
        TimeSpan holdDuration,
        CancellationToken ct);

    Task<FinalizeOrderStoredProcedureResult> FinalizeOrderAsync(
        Guid holdId,
        Guid userId,
        string idempotencyKey,
        CancellationToken ct);

    Task<ReleaseExpiredHoldsStoredProcedureResult> ReleaseExpiredHoldsAsync(CancellationToken ct);
}
