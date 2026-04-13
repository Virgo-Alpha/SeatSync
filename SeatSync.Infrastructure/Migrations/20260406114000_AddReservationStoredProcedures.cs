using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SeatSync.Infrastructure.Data;

#nullable disable

namespace SeatSync.Infrastructure.Migrations;

[DbContext(typeof(SeatSyncDbContext))]
[Migration("20260406114000_AddReservationStoredProcedures")]
public partial class AddReservationStoredProcedures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF TYPE_ID(N'dbo.GuidList') IS NULL
            BEGIN
                CREATE TYPE dbo.GuidList AS TABLE
                (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY
                );
            END
            """);

        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.Orders')
                  AND name = N'IdempotencyKey'
                  AND max_length = -1
            )
            BEGIN
                ALTER TABLE dbo.Orders
                ALTER COLUMN IdempotencyKey NVARCHAR(200) NOT NULL;
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeatStatuses_EventId_SeatId' AND object_id = OBJECT_ID(N'dbo.SeatStatuses'))
            BEGIN
                CREATE UNIQUE INDEX IX_SeatStatuses_EventId_SeatId
                ON dbo.SeatStatuses (EventId, SeatId);
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Holds_State_ExpiresAt' AND object_id = OBJECT_ID(N'dbo.Holds'))
            BEGIN
                CREATE INDEX IX_Holds_State_ExpiresAt
                ON dbo.Holds (State, ExpiresAt);
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeatStatuses_HoldId_State' AND object_id = OBJECT_ID(N'dbo.SeatStatuses'))
            BEGIN
                CREATE INDEX IX_SeatStatuses_HoldId_State
                ON dbo.SeatStatuses (HoldId, State);
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeatStatuses_OrderId_State' AND object_id = OBJECT_ID(N'dbo.SeatStatuses'))
            BEGIN
                CREATE INDEX IX_SeatStatuses_OrderId_State
                ON dbo.SeatStatuses (OrderId, State);
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_UserId_IdempotencyKey' AND object_id = OBJECT_ID(N'dbo.Orders'))
            BEGIN
                CREATE UNIQUE INDEX IX_Orders_UserId_IdempotencyKey
                ON dbo.Orders (UserId, IdempotencyKey);
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE dbo.sp_ReleaseExpiredHolds
                @NowUtc DATETIMEOFFSET(7) = NULL,
                @ReleasedCount INT OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @now DATETIMEOFFSET(7) = COALESCE(@NowUtc, SYSUTCDATETIME());
                SET @ReleasedCount = 0;

                DECLARE @ExpiredHoldIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);

                BEGIN TRAN;

                UPDATE h
                SET h.State = 1
                OUTPUT inserted.Id INTO @ExpiredHoldIds(Id)
                FROM dbo.Holds h WITH (UPDLOCK, ROWLOCK)
                WHERE h.State = 0
                  AND h.ExpiresAt <= @now;

                UPDATE ss
                SET ss.State = 0,
                    ss.HoldId = NULL,
                    ss.OrderId = NULL,
                    ss.UpdatedAt = @now
                FROM dbo.SeatStatuses ss WITH (UPDLOCK, ROWLOCK)
                INNER JOIN @ExpiredHoldIds expired ON expired.Id = ss.HoldId
                WHERE ss.State = 1;

                SET @ReleasedCount = @@ROWCOUNT;

                COMMIT TRAN;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE dbo.sp_CreateSeatHold
                @EventId UNIQUEIDENTIFIER,
                @UserId UNIQUEIDENTIFIER,
                @HoldDurationMinutes INT,
                @SeatIds dbo.GuidList READONLY,
                @ResultCode INT OUTPUT,
                @HoldId UNIQUEIDENTIFIER OUTPUT,
                @ExpiresAt DATETIMEOFFSET(7) OUTPUT,
                @Message NVARCHAR(200) OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @now DATETIMEOFFSET(7) = SYSUTCDATETIME();
                DECLARE @released INT = 0;

                SET @ResultCode = 1;
                SET @HoldId = NULL;
                SET @ExpiresAt = NULL;
                SET @Message = N'Validation failed.';

                EXEC dbo.sp_ReleaseExpiredHolds
                    @NowUtc = @now,
                    @ReleasedCount = @released OUTPUT;

                IF @HoldDurationMinutes <= 0
                BEGIN
                    SET @Message = N'Hold duration must be positive.';
                    RETURN;
                END;

                IF NOT EXISTS (SELECT 1 FROM @SeatIds)
                BEGIN
                    SET @Message = N'At least one seat is required.';
                    RETURN;
                END;

                SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                BEGIN TRAN;

                DECLARE @requestedSeatCount INT = (SELECT COUNT(1) FROM @SeatIds);
                DECLARE @matchedSeatCount INT;
                DECLARE @unavailableCount INT;

                SELECT
                    @matchedSeatCount = COUNT(1),
                    @unavailableCount = SUM(CASE WHEN ss.State <> 0 THEN 1 ELSE 0 END)
                FROM dbo.SeatStatuses ss WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN @SeatIds seatInput ON seatInput.Id = ss.SeatId
                WHERE ss.EventId = @EventId;

                IF @matchedSeatCount <> @requestedSeatCount
                BEGIN
                    ROLLBACK TRAN;
                    SET @ResultCode = 2;
                    SET @Message = N'One or more seats were not found.';
                    RETURN;
                END;

                IF @unavailableCount > 0
                BEGIN
                    ROLLBACK TRAN;
                    SET @ResultCode = 3;
                    SET @Message = N'One or more seats are not available.';
                    RETURN;
                END;

                SET @HoldId = NEWID();
                SET @ExpiresAt = DATEADD(MINUTE, @HoldDurationMinutes, @now);

                INSERT INTO dbo.Holds (Id, EventId, UserId, CreatedAt, ExpiresAt, State)
                VALUES (@HoldId, @EventId, @UserId, @now, @ExpiresAt, 0);

                UPDATE ss
                SET ss.State = 1,
                    ss.HoldId = @HoldId,
                    ss.OrderId = NULL,
                    ss.UpdatedAt = @now
                FROM dbo.SeatStatuses ss
                INNER JOIN @SeatIds seatInput ON seatInput.Id = ss.SeatId
                WHERE ss.EventId = @EventId;

                COMMIT TRAN;

                SET @ResultCode = 0;
                SET @Message = N'Hold created.';
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE dbo.sp_FinalizeSeatOrder
                @HoldId UNIQUEIDENTIFIER,
                @UserId UNIQUEIDENTIFIER,
                @IdempotencyKey NVARCHAR(200),
                @ResultCode INT OUTPUT,
                @OrderId UNIQUEIDENTIFIER OUTPUT,
                @Message NVARCHAR(200) OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @now DATETIMEOFFSET(7) = SYSUTCDATETIME();
                DECLARE @eventId UNIQUEIDENTIFIER;
                DECLARE @holdState INT;
                DECLARE @holdOwner UNIQUEIDENTIFIER;
                DECLARE @expiresAt DATETIMEOFFSET(7);
                DECLARE @released INT = 0;

                SET @ResultCode = 1;
                SET @OrderId = NULL;
                SET @Message = N'Validation failed.';

                EXEC dbo.sp_ReleaseExpiredHolds
                    @NowUtc = @now,
                    @ReleasedCount = @released OUTPUT;

                IF @IdempotencyKey IS NULL OR LTRIM(RTRIM(@IdempotencyKey)) = N''
                BEGIN
                    SET @Message = N'Idempotency key is required.';
                    RETURN;
                END;

                SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                BEGIN TRAN;

                SELECT @OrderId = o.Id
                FROM dbo.Orders o WITH (UPDLOCK, HOLDLOCK)
                WHERE o.UserId = @UserId
                  AND o.IdempotencyKey = @IdempotencyKey;

                IF @OrderId IS NOT NULL
                BEGIN
                    COMMIT TRAN;
                    SET @ResultCode = 0;
                    SET @Message = N'Order already finalized.';
                    RETURN;
                END;

                SELECT
                    @eventId = h.EventId,
                    @holdState = h.State,
                    @holdOwner = h.UserId,
                    @expiresAt = h.ExpiresAt
                FROM dbo.Holds h WITH (UPDLOCK, HOLDLOCK)
                WHERE h.Id = @HoldId;

                IF @eventId IS NULL
                BEGIN
                    ROLLBACK TRAN;
                    SET @ResultCode = 2;
                    SET @Message = N'Hold not found.';
                    RETURN;
                END;

                IF @holdOwner <> @UserId
                BEGIN
                    ROLLBACK TRAN;
                    SET @ResultCode = 4;
                    SET @Message = N'Hold belongs to a different user.';
                    RETURN;
                END;

                IF @holdState <> 0 OR @expiresAt <= @now
                BEGIN
                    IF @expiresAt <= @now AND @holdState = 0
                    BEGIN
                        UPDATE dbo.Holds
                        SET State = 1
                        WHERE Id = @HoldId
                          AND State = 0;

                        UPDATE dbo.SeatStatuses
                        SET State = 0,
                            HoldId = NULL,
                            OrderId = NULL,
                            UpdatedAt = @now
                        WHERE HoldId = @HoldId
                          AND State = 1;
                    END;

                    COMMIT TRAN;
                    SET @ResultCode = 3;
                    SET @Message = N'Hold is no longer active.';
                    RETURN;
                END;

                DECLARE @SeatCount INT;
                SELECT @SeatCount = COUNT(1)
                FROM dbo.SeatStatuses ss WITH (UPDLOCK, HOLDLOCK)
                WHERE ss.HoldId = @HoldId
                  AND ss.State = 1;

                IF @SeatCount IS NULL OR @SeatCount = 0
                BEGIN
                    ROLLBACK TRAN;
                    SET @ResultCode = 3;
                    SET @Message = N'No held seats found for this hold.';
                    RETURN;
                END;

                SET @OrderId = NEWID();

                INSERT INTO dbo.Orders
                (
                    Id,
                    State,
                    IdempotencyKey,
                    CreatedAt,
                    CompletedAt,
                    EventId,
                    UserId,
                    TotalAmount,
                    Currency
                )
                VALUES
                (
                    @OrderId,
                    0,
                    @IdempotencyKey,
                    @now,
                    NULL,
                    @eventId,
                    @UserId,
                    CAST(@SeatCount AS DECIMAL(18, 2)) * 100.00,
                    N'USD'
                );

                UPDATE ss
                SET ss.State = 2,
                    ss.HoldId = NULL,
                    ss.OrderId = @OrderId,
                    ss.UpdatedAt = @now
                FROM dbo.SeatStatuses ss
                WHERE ss.HoldId = @HoldId
                  AND ss.State = 1;

                INSERT INTO dbo.Tickets
                (
                    Id,
                    OrderId,
                    SeatId,
                    EventId,
                    JwtId,
                    IssuedAt,
                    RedeemedAt
                )
                SELECT
                    NEWID(),
                    @OrderId,
                    ss.SeatId,
                    @eventId,
                    CONVERT(NVARCHAR(36), NEWID()),
                    @now,
                    NULL
                FROM dbo.SeatStatuses ss
                WHERE ss.OrderId = @OrderId
                  AND ss.State = 2;

                UPDATE dbo.Holds
                SET State = 2
                WHERE Id = @HoldId;

                COMMIT TRAN;

                SET @ResultCode = 0;
                SET @Message = N'Order finalized.';
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_FinalizeSeatOrder;");
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CreateSeatHold;");
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ReleaseExpiredHolds;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Orders_UserId_IdempotencyKey ON dbo.Orders;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_SeatStatuses_OrderId_State ON dbo.SeatStatuses;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_SeatStatuses_HoldId_State ON dbo.SeatStatuses;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Holds_State_ExpiresAt ON dbo.Holds;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_SeatStatuses_EventId_SeatId ON dbo.SeatStatuses;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.GuidList;");
    }
}
