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

public class OrdersControllerBehaviorTests
{
    [Fact]
    public async Task MockCapturePayment_Should_Return_NotFound_When_Order_Missing()
    {
        await using var db = CreateDbContext();
        var sut = NewController(db, Guid.NewGuid(), "Attendee");

        var response = await sut.MockCapturePayment(Guid.NewGuid(), new MockPaymentRequest(true), CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task MockCapturePayment_Should_Return_Forbid_For_NonOwner_Attendee()
    {
        await using var db = CreateDbContext();
        var order = await SeedOrderAsync(db, PaymentState.Pending, ownerUserId: Guid.NewGuid());
        var sut = NewController(db, Guid.NewGuid(), "Attendee");

        var response = await sut.MockCapturePayment(order.Id, new MockPaymentRequest(true), CancellationToken.None);

        response.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task MockCapturePayment_Should_Return_Already_Captured_When_Captured()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var order = await SeedOrderAsync(db, PaymentState.Captured, owner);
        var sut = NewController(db, owner, "Attendee");

        var response = await sut.MockCapturePayment(order.Id, new MockPaymentRequest(true), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task MockCapturePayment_Should_Capture_When_Pending_And_Success()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var order = await SeedOrderAsync(db, PaymentState.Pending, owner);
        var sut = NewController(db, owner, "Attendee");

        var response = await sut.MockCapturePayment(order.Id, new MockPaymentRequest(true), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        var reloaded = await db.Orders.SingleAsync(x => x.Id == order.Id);
        reloaded.State.Should().Be(PaymentState.Captured);
        reloaded.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MockCapturePayment_Should_Mark_Failed_When_Request_Fails()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var order = await SeedOrderAsync(db, PaymentState.Pending, owner);
        var sut = NewController(db, owner, "Attendee");

        var response = await sut.MockCapturePayment(order.Id, new MockPaymentRequest(false), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        var reloaded = await db.Orders.SingleAsync(x => x.Id == order.Id);
        reloaded.State.Should().Be(PaymentState.Failed);
    }

    [Fact]
    public async Task DownloadReceipt_Should_Return_NotFound_When_Order_Missing()
    {
        await using var db = CreateDbContext();
        var sut = NewController(db, Guid.NewGuid(), "Attendee");

        var response = await sut.DownloadReceipt(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DownloadReceipt_Should_Return_Text_File()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var order = await SeedOrderAsync(db, PaymentState.Captured, owner);
        var sut = NewController(db, owner, "Attendee");

        var response = await sut.DownloadReceipt(order.Id, CancellationToken.None);

        response.Should().BeOfType<FileContentResult>()
            .Which.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task EmailReceipt_Should_Return_NotFound_When_Order_Missing()
    {
        await using var db = CreateDbContext();
        var sut = NewController(db, Guid.NewGuid(), "Attendee");

        var response = await sut.EmailReceipt(Guid.NewGuid(), new EmailReceiptRequest(null), CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task EmailReceipt_Should_Return_Forbid_For_Non_Owner_Attendee()
    {
        await using var db = CreateDbContext();
        var order = await SeedOrderAsync(db, PaymentState.Captured, Guid.NewGuid());
        var sut = NewController(db, Guid.NewGuid(), "Attendee");

        var response = await sut.EmailReceipt(order.Id, new EmailReceiptRequest(null), CancellationToken.None);

        response.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task EmailReceipt_Should_Use_Default_User_Email_When_Request_Empty()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var order = await SeedOrderAsync(db, PaymentState.Captured, owner, ownerEmail: "owner@test.dev");
        var sut = NewController(db, owner, "Attendee");

        var response = await sut.EmailReceipt(order.Id, new EmailReceiptRequest("   "), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task EmailReceipt_Should_Allow_Admin_For_Non_Owned_Order()
    {
        await using var db = CreateDbContext();
        var order = await SeedOrderAsync(db, PaymentState.Captured, Guid.NewGuid());
        var sut = NewController(db, Guid.NewGuid(), "Admin");

        var response = await sut.EmailReceipt(order.Id, new EmailReceiptRequest(" ops@test.dev "), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private static OrdersController NewController(SeatSyncDbContext db, Guid userId, string role)
    {
        var sut = new OrdersController(
            new FakeReservationStoredProcedureService(),
            db,
            TimeProvider.System,
            NullLogger<OrdersController>.Instance);

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, role)
        ], "Test");

        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return sut;
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

    private static async Task<Order> SeedOrderAsync(
        SeatSyncDbContext db,
        PaymentState state,
        Guid ownerUserId,
        string ownerEmail = "owner@test.dev")
    {
        var ev = new Event("Order Event", DateTimeOffset.UtcNow.AddDays(2), "Agenda", ownerUserId);
        var seat = new Seat(ev.Id, "Main", "A", "1", 1, 1);
        var status = new SeatStatus(ev.Id, seat.Id, SeatState.Available);
        var order = new Order(
            ev.Id,
            ownerUserId,
            50m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            [seat.Id]);

        if (state == PaymentState.Authorized)
        {
            order.MarkAuthorized();
        }
        else if (state == PaymentState.Captured)
        {
            order.MarkAuthorized();
            order.MarkCaptured(TimeProvider.System);
            status.MarkHeld(Guid.NewGuid());
            status.MarkSold(order.Id);
        }

        db.Events.Add(ev);
        db.AppUsers.Add(new AppUser(ownerUserId, ownerEmail, "Owner User", "pw", UserRole.Attendee));
        db.Seats.Add(seat);
        db.SeatStatuses.Add(status);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order;
    }

    private sealed class FakeReservationStoredProcedureService : IReservationStoredProcedureService
    {
        public Task<CreateHoldStoredProcedureResult> CreateSeatHoldAsync(
            Guid eventId,
            Guid userId,
            IReadOnlyCollection<Guid> seatIds,
            TimeSpan holdDuration,
            CancellationToken ct) =>
            Task.FromResult(new CreateHoldStoredProcedureResult(
                ReservationResultCode.Success,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(10),
                "ok"));

        public Task<FinalizeOrderStoredProcedureResult> FinalizeOrderAsync(
            Guid holdId,
            Guid userId,
            string idempotencyKey,
            CancellationToken ct) =>
            Task.FromResult(new FinalizeOrderStoredProcedureResult(
                ReservationResultCode.Success,
                Guid.NewGuid(),
                "ok"));

        public Task<ReleaseExpiredHoldsStoredProcedureResult> ReleaseExpiredHoldsAsync(CancellationToken ct) =>
            Task.FromResult(new ReleaseExpiredHoldsStoredProcedureResult(0));
    }
}
