using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Infrastructure.Services;

public sealed class ReservationStoredProcedureService : IReservationStoredProcedureService
{
    private readonly SeatSyncDbContext _db;

    public ReservationStoredProcedureService(SeatSyncDbContext db)
    {
        _db = db;
    }

    public async Task<CreateHoldStoredProcedureResult> CreateSeatHoldAsync(
        Guid eventId,
        Guid userId,
        IReadOnlyCollection<Guid> seatIds,
        TimeSpan holdDuration,
        CancellationToken ct)
    {
        if (seatIds.Count == 0)
        {
            return new CreateHoldStoredProcedureResult(
                ReservationResultCode.ValidationError,
                null,
                null,
                "At least one seat is required.");
        }

        var holdId = Guid.Empty;
        DateTimeOffset? expiresAt = null;
        var message = string.Empty;
        var resultCode = ReservationResultCode.ValidationError;

        await using var connection = new SqlConnection(GetRequiredConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("dbo.sp_CreateSeatHold", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@EventId", eventId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HoldDurationMinutes", (int)Math.Max(1, Math.Ceiling(holdDuration.TotalMinutes)));

        var seatIdsTable = new DataTable();
        seatIdsTable.Columns.Add("Id", typeof(Guid));
        foreach (var seatId in seatIds.Distinct())
        {
            seatIdsTable.Rows.Add(seatId);
        }

        var seatIdsParameter = command.Parameters.AddWithValue("@SeatIds", seatIdsTable);
        seatIdsParameter.SqlDbType = SqlDbType.Structured;
        seatIdsParameter.TypeName = "dbo.GuidList";

        var resultCodeParameter = new SqlParameter("@ResultCode", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(resultCodeParameter);

        var holdIdParameter = new SqlParameter("@HoldId", SqlDbType.UniqueIdentifier)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(holdIdParameter);

        var expiresAtParameter = new SqlParameter("@ExpiresAt", SqlDbType.DateTimeOffset)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(expiresAtParameter);

        var messageParameter = new SqlParameter("@Message", SqlDbType.NVarChar, 200)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(messageParameter);

        await command.ExecuteNonQueryAsync(ct);

        if (resultCodeParameter.Value is int rawCode)
        {
            resultCode = Enum.IsDefined(typeof(ReservationResultCode), rawCode)
                ? (ReservationResultCode)rawCode
                : ReservationResultCode.ValidationError;
        }

        if (holdIdParameter.Value is Guid parsedHoldId)
        {
            holdId = parsedHoldId;
        }

        if (expiresAtParameter.Value is DateTimeOffset parsedExpiresAt)
        {
            expiresAt = parsedExpiresAt;
        }

        if (messageParameter.Value is string parsedMessage)
        {
            message = parsedMessage;
        }

        return new CreateHoldStoredProcedureResult(
            resultCode,
            holdId == Guid.Empty ? null : holdId,
            expiresAt,
            message);
    }

    public async Task<FinalizeOrderStoredProcedureResult> FinalizeOrderAsync(
        Guid holdId,
        Guid userId,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(GetRequiredConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("dbo.sp_FinalizeSeatOrder", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@HoldId", holdId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@IdempotencyKey", idempotencyKey);

        var resultCodeParameter = new SqlParameter("@ResultCode", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(resultCodeParameter);

        var orderIdParameter = new SqlParameter("@OrderId", SqlDbType.UniqueIdentifier)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(orderIdParameter);

        var messageParameter = new SqlParameter("@Message", SqlDbType.NVarChar, 200)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(messageParameter);

        await command.ExecuteNonQueryAsync(ct);

        var resultCode = ReservationResultCode.ValidationError;
        if (resultCodeParameter.Value is int rawCode)
        {
            resultCode = Enum.IsDefined(typeof(ReservationResultCode), rawCode)
                ? (ReservationResultCode)rawCode
                : ReservationResultCode.ValidationError;
        }

        Guid? orderId = orderIdParameter.Value is Guid parsedOrderId && parsedOrderId != Guid.Empty
            ? parsedOrderId
            : null;

        var message = messageParameter.Value as string ?? string.Empty;

        return new FinalizeOrderStoredProcedureResult(resultCode, orderId, message);
    }

    public async Task<ReleaseExpiredHoldsStoredProcedureResult> ReleaseExpiredHoldsAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(GetRequiredConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("dbo.sp_ReleaseExpiredHolds", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var releasedCountParameter = new SqlParameter("@ReleasedCount", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(releasedCountParameter);

        await command.ExecuteNonQueryAsync(ct);

        var releasedCount = releasedCountParameter.Value is int parsed ? parsed : 0;
        return new ReleaseExpiredHoldsStoredProcedureResult(releasedCount);
    }

    private string GetRequiredConnectionString() =>
        _db.Database.GetConnectionString()
        ?? throw new InvalidOperationException("SeatSyncDb connection string is not configured.");
}
