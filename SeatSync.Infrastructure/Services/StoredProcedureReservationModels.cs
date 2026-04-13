namespace SeatSync.Infrastructure.Services;

public enum ReservationResultCode
{
    Success = 0,
    ValidationError = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4
}

public sealed record CreateHoldStoredProcedureResult(
    ReservationResultCode ResultCode,
    Guid? HoldId,
    DateTimeOffset? ExpiresAt,
    string Message);

public sealed record FinalizeOrderStoredProcedureResult(
    ReservationResultCode ResultCode,
    Guid? OrderId,
    string Message);

public sealed record ReleaseExpiredHoldsStoredProcedureResult(
    int ReleasedCount);
