using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatSync.Api.Controllers;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;
using SeatSync.Infrastructure.Services;

namespace SeatSync.Tests.Api;

public class ReservationControllerMappingTests
{
    [Fact]
    public async Task HoldsController_Should_Map_Conflict_To_409()
    {
        var sut = new HoldsController(new FakeReservationStoredProcedureService
        {
            CreateHoldResult = new CreateHoldStoredProcedureResult(
                ReservationResultCode.Conflict,
                null,
                null,
                "One or more seats are not available.")
        });
        SetUser(sut, Guid.NewGuid());

        var response = await sut.CreateHold(
            new CreateHoldRequest(Guid.NewGuid(), [Guid.NewGuid()]),
            CancellationToken.None);

        response.Result.Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task HoldsController_Should_Map_NotFound_To_404()
    {
        var sut = new HoldsController(new FakeReservationStoredProcedureService
        {
            CreateHoldResult = new CreateHoldStoredProcedureResult(
                ReservationResultCode.NotFound,
                null,
                null,
                "One or more seats were not found.")
        });
        SetUser(sut, Guid.NewGuid());

        var response = await sut.CreateHold(
            new CreateHoldRequest(Guid.NewGuid(), [Guid.NewGuid()]),
            CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task OrdersController_Should_Map_Conflict_To_409()
    {
        await using var db = CreateDbContext();
        var sut = new OrdersController(
            new FakeReservationStoredProcedureService
            {
                FinalizeOrderResult = new FinalizeOrderStoredProcedureResult(
                    ReservationResultCode.Conflict,
                    null,
                    "Hold is no longer active.")
            },
            db,
            TimeProvider.System,
            NullLogger<OrdersController>.Instance);
        SetUser(sut, Guid.NewGuid());

        var response = await sut.FinalizeOrder(
            new FinalizeOrderRequest(Guid.NewGuid(), "idem-1"),
            CancellationToken.None);

        response.Result.Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task OrdersController_Should_Map_Success_To_200()
    {
        var orderId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var sut = new OrdersController(
            new FakeReservationStoredProcedureService
            {
                FinalizeOrderResult = new FinalizeOrderStoredProcedureResult(
                    ReservationResultCode.Success,
                    orderId,
                    "Order finalized.")
            },
            db,
            TimeProvider.System,
            NullLogger<OrdersController>.Instance);
        SetUser(sut, Guid.NewGuid());

        var response = await sut.FinalizeOrder(
            new FinalizeOrderRequest(Guid.NewGuid(), "idem-2"),
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task OrdersController_DownloadReceiptPdf_Should_Return_File_For_Order_Owner()
    {
        await using var db = CreateDbContext();
        var order = SeedCapturedOrder(db, out var userId);

        var sut = new OrdersController(
            new FakeReservationStoredProcedureService(),
            db,
            TimeProvider.System,
            NullLogger<OrdersController>.Instance);
        SetUser(sut, userId);

        var response = await sut.DownloadReceiptPdf(order.Id, CancellationToken.None);

        response.Should().BeOfType<FileContentResult>()
            .Which.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task OrdersController_DownloadReceiptPdf_Should_Return_File_For_NonOwner_When_Using_Receipt_Number()
    {
        await using var db = CreateDbContext();
        var order = SeedCapturedOrder(db, out _);

        var sut = new OrdersController(
            new FakeReservationStoredProcedureService(),
            db,
            TimeProvider.System,
            NullLogger<OrdersController>.Instance);
        SetUser(sut, Guid.NewGuid());

        var response = await sut.DownloadReceiptPdf(order.Id, CancellationToken.None);

        response.Should().BeOfType<FileContentResult>()
            .Which.ContentType.Should().Be("application/pdf");
    }

    private sealed class FakeReservationStoredProcedureService : IReservationStoredProcedureService
    {
        public CreateHoldStoredProcedureResult CreateHoldResult { get; set; } =
            new(ReservationResultCode.Success, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10), "ok");

        public FinalizeOrderStoredProcedureResult FinalizeOrderResult { get; set; } =
            new(ReservationResultCode.Success, Guid.NewGuid(), "ok");

        public ReleaseExpiredHoldsStoredProcedureResult ReleaseResult { get; set; } =
            new(0);

        public Task<CreateHoldStoredProcedureResult> CreateSeatHoldAsync(
            Guid eventId,
            Guid userId,
            IReadOnlyCollection<Guid> seatIds,
            TimeSpan holdDuration,
            CancellationToken ct) =>
            Task.FromResult(CreateHoldResult);

        public Task<FinalizeOrderStoredProcedureResult> FinalizeOrderAsync(
            Guid holdId,
            Guid userId,
            string idempotencyKey,
            CancellationToken ct) =>
            Task.FromResult(FinalizeOrderResult);

        public Task<ReleaseExpiredHoldsStoredProcedureResult> ReleaseExpiredHoldsAsync(CancellationToken ct) =>
            Task.FromResult(ReleaseResult);
    }

    private static SeatSyncDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SeatSyncDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SeatSyncDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static void SetUser(ControllerBase controller, Guid userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Attendee")
        ], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private static Order SeedCapturedOrder(SeatSyncDbContext db, out Guid userId)
    {
        userId = Guid.NewGuid();
        var ev = new Event("Test Event", DateTimeOffset.UtcNow.AddDays(2), "Agenda", userId);
        var eventId = ev.Id;
        var seat = new Seat(eventId, "Main", "A", "1", 1, 1);
        var status = new SeatStatus(eventId, seat.Id, SeatState.Available);
        var order = new Order(
            eventId,
            userId,
            totalAmount: 50m,
            currency: "USD",
            idempotencyKey: $"test-{Guid.NewGuid():N}",
            seatIds: [seat.Id]);
        order.MarkAuthorized();
        order.MarkCaptured(TimeProvider.System);

        status.MarkHeld(Guid.NewGuid());
        status.MarkSold(order.Id);

        db.Events.Add(ev);
        db.AppUsers.Add(new AppUser(userId, "user@test.dev", "Test User", "pw", UserRole.Attendee));
        db.Seats.Add(seat);
        db.SeatStatuses.Add(status);
        db.Orders.Add(order);
        db.SaveChanges();

        return order;
    }
}
