using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Services;
using SeatSync.Tests.Infrastructure;

namespace SeatSync.Tests.Infrastructure;

public sealed class ReservationStoredProcedureIntegrationTests
    : IClassFixture<SqlServerReservationTestFixture>
{
    private readonly SqlServerReservationTestFixture _fixture;

    public ReservationStoredProcedureIntegrationTests(SqlServerReservationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateSeatHold_Should_Hold_Single_Seat()
    {
        if (!_fixture.EnsureAvailable()) return;
        var setup = await SeedEventWithSeatsAsync(1);

        await using var db = _fixture.CreateDbContext();
        var sut = new ReservationStoredProcedureService(db);

        var result = await sut.CreateSeatHoldAsync(
            setup.EventId,
            setup.UserId,
            [setup.SeatIds[0]],
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        result.ResultCode.Should().Be(ReservationResultCode.Success);
        result.HoldId.Should().NotBeNull();

        var status = await db.SeatStatuses
            .AsNoTracking()
            .SingleAsync(x => x.EventId == setup.EventId && x.SeatId == setup.SeatIds[0]);

        status.State.Should().Be(SeatState.Held);
        status.HoldId.Should().Be(result.HoldId);
    }

    [Fact]
    public async Task CreateSeatHold_Should_Be_Atomic_For_Multiple_Seats()
    {
        if (!_fixture.EnsureAvailable()) return;
        var setup = await SeedEventWithSeatsAsync(3);

        await using var db = _fixture.CreateDbContext();
        var sut = new ReservationStoredProcedureService(db);

        var firstHold = await sut.CreateSeatHoldAsync(
            setup.EventId,
            setup.UserId,
            [setup.SeatIds[0]],
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        firstHold.ResultCode.Should().Be(ReservationResultCode.Success);

        var secondUser = Guid.NewGuid();
        var atomicAttempt = await sut.CreateSeatHoldAsync(
            setup.EventId,
            secondUser,
            [setup.SeatIds[0], setup.SeatIds[1], setup.SeatIds[2]],
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        atomicAttempt.ResultCode.Should().Be(ReservationResultCode.Conflict);

        var statuses = await db.SeatStatuses
            .AsNoTracking()
            .Where(x => x.EventId == setup.EventId)
            .ToListAsync();

        statuses.Single(x => x.SeatId == setup.SeatIds[0]).State.Should().Be(SeatState.Held);
        statuses.Single(x => x.SeatId == setup.SeatIds[1]).State.Should().Be(SeatState.Available);
        statuses.Single(x => x.SeatId == setup.SeatIds[2]).State.Should().Be(SeatState.Available);
    }

    [Fact]
    public async Task CreateSeatHold_Should_Conflict_For_Concurrent_Requests_On_Same_Seat()
    {
        if (!_fixture.EnsureAvailable()) return;
        var setup = await SeedEventWithSeatsAsync(1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        var firstTask = Task.Run(async () =>
        {
            await gate.Task;
            await using var db = _fixture.CreateDbContext();
            var sut = new ReservationStoredProcedureService(db);
            return await sut.CreateSeatHoldAsync(
                setup.EventId,
                firstUser,
                [setup.SeatIds[0]],
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
        });

        var secondTask = Task.Run(async () =>
        {
            await gate.Task;
            await using var db = _fixture.CreateDbContext();
            var sut = new ReservationStoredProcedureService(db);
            return await sut.CreateSeatHoldAsync(
                setup.EventId,
                secondUser,
                [setup.SeatIds[0]],
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
        });

        gate.SetResult();
        var results = await Task.WhenAll(firstTask, secondTask);

        results.Count(x => x.ResultCode == ReservationResultCode.Success).Should().Be(1);
        results.Count(x => x.ResultCode == ReservationResultCode.Conflict).Should().Be(1);
    }

    [Fact]
    public async Task FinalizeOrder_Should_Be_Idempotent_For_Repeated_Retries()
    {
        if (!_fixture.EnsureAvailable()) return;
        var setup = await SeedEventWithSeatsAsync(2);

        await using var db = _fixture.CreateDbContext();
        var sut = new ReservationStoredProcedureService(db);
        var hold = await sut.CreateSeatHoldAsync(
            setup.EventId,
            setup.UserId,
            setup.SeatIds,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        hold.ResultCode.Should().Be(ReservationResultCode.Success);
        hold.HoldId.Should().NotBeNull();

        var key = $"idem-{Guid.NewGuid():N}";
        var first = await sut.FinalizeOrderAsync(hold.HoldId!.Value, setup.UserId, key, CancellationToken.None);
        var second = await sut.FinalizeOrderAsync(hold.HoldId.Value, setup.UserId, key, CancellationToken.None);

        first.ResultCode.Should().Be(ReservationResultCode.Success);
        second.ResultCode.Should().Be(ReservationResultCode.Success);
        first.OrderId.Should().NotBeNull();
        second.OrderId.Should().Be(first.OrderId);
    }

    [Fact]
    public async Task FinalizeOrder_Should_Return_Conflict_For_Expired_Hold()
    {
        if (!_fixture.EnsureAvailable()) return;
        var setup = await SeedEventWithSeatsAsync(1);

        await using var db = _fixture.CreateDbContext();
        var sut = new ReservationStoredProcedureService(db);

        var hold = await sut.CreateSeatHoldAsync(
            setup.EventId,
            setup.UserId,
            [setup.SeatIds[0]],
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        hold.ResultCode.Should().Be(ReservationResultCode.Success);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.Holds SET ExpiresAt = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE Id = {hold.HoldId!.Value}");

        var result = await sut.FinalizeOrderAsync(
            hold.HoldId!.Value,
            setup.UserId,
            $"idem-{Guid.NewGuid():N}",
            CancellationToken.None);

        result.ResultCode.Should().Be(ReservationResultCode.Conflict);
        result.OrderId.Should().BeNull();

        var refreshedSeat = await db.SeatStatuses.AsNoTracking()
            .SingleAsync(x => x.EventId == setup.EventId && x.SeatId == setup.SeatIds[0]);
        refreshedSeat.State.Should().Be(SeatState.Available);
    }

    [Fact]
    public async Task ReleaseExpiredHolds_Should_Free_Seat_And_Expire_Hold()
    {
        if (!_fixture.EnsureAvailable()) return;
        var setup = await SeedEventWithSeatsAsync(1);

        await using var db = _fixture.CreateDbContext();
        var sut = new ReservationStoredProcedureService(db);

        var hold = await sut.CreateSeatHoldAsync(
            setup.EventId,
            setup.UserId,
            [setup.SeatIds[0]],
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        hold.ResultCode.Should().Be(ReservationResultCode.Success);
        hold.HoldId.Should().NotBeNull();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.Holds SET ExpiresAt = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE Id = {hold.HoldId!.Value}");

        var release = await sut.ReleaseExpiredHoldsAsync(CancellationToken.None);
        release.ReleasedCount.Should().BeGreaterThan(0);

        var refreshedHold = await db.Holds.AsNoTracking().SingleAsync(x => x.Id == hold.HoldId.Value);
        var refreshedSeat = await db.SeatStatuses.AsNoTracking()
            .SingleAsync(x => x.EventId == setup.EventId && x.SeatId == setup.SeatIds[0]);

        refreshedHold.State.Should().Be(HoldState.Expired);
        refreshedSeat.State.Should().Be(SeatState.Available);
        refreshedSeat.HoldId.Should().BeNull();
    }

    private async Task<(Guid EventId, Guid UserId, List<Guid> SeatIds)> SeedEventWithSeatsAsync(int seatCount)
    {
        await using var db = _fixture.CreateDbContext();
        var ev = new Event($"SQL Integration {Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(2));
        var userId = Guid.NewGuid();
        var seatIds = new List<Guid>(seatCount);

        db.Events.Add(ev);
        for (var i = 0; i < seatCount; i++)
        {
            var seat = new Seat(ev.Id, "Orchestra", "A", (i + 1).ToString(), i + 1, 1);
            db.Seats.Add(seat);
            db.SeatStatuses.Add(new SeatStatus(ev.Id, seat.Id, SeatState.Available));
            seatIds.Add(seat.Id);
        }

        await db.SaveChangesAsync();
        return (ev.Id, userId, seatIds);
    }
}
