using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatSync.Api.Controllers;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Tests.Api;

public class TicketsControllerTests
{
    [Fact]
    public async Task Redeem_Should_Return_404_When_Ticket_Not_Found()
    {
        await using var db = CreateDbContext();
        var sut = new TicketsController(db);

        var response = await sut.Redeem(
            new RedeemTicketRequest("missing-ticket"),
            CancellationToken.None);

        response.Should().BeOfType<NotFoundResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Redeem_Should_Return_400_When_Ticket_Already_Redeemed()
    {
        await using var db = CreateDbContext();
        var ticket = SeedTicket(db);
        ticket.Redeem(DateTimeOffset.UtcNow.AddMinutes(-10));
        await db.SaveChangesAsync();

        var sut = new TicketsController(db);
        var response = await sut.Redeem(
            new RedeemTicketRequest(ticket.JwtId),
            CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Redeem_Should_Return_200_And_Mark_Redeemed_When_Valid()
    {
        await using var db = CreateDbContext();
        var ticket = SeedTicket(db);

        var sut = new TicketsController(db);
        var response = await sut.Redeem(
            new RedeemTicketRequest(ticket.JwtId),
            CancellationToken.None);

        response.Should().BeOfType<OkResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);

        var reloaded = await db.Tickets.SingleAsync(x => x.Id == ticket.Id);
        reloaded.RedeemedAt.Should().NotBeNull();
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

    private static Ticket SeedTicket(SeatSyncDbContext db)
    {
        var userId = Guid.NewGuid();
        var ev = new Event("Ticket Event", DateTimeOffset.UtcNow.AddDays(1), "Agenda", userId);
        var seat = new Seat(ev.Id, "Main", "A", "1", 1, 1);
        var status = new SeatStatus(ev.Id, seat.Id, SeatState.Available);
        var order = new Order(
            ev.Id,
            userId,
            100m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            [seat.Id]);
        order.MarkAuthorized();
        order.MarkCaptured(TimeProvider.System);
        status.MarkHeld(Guid.NewGuid());
        status.MarkSold(order.Id);

        var ticket = new Ticket(order.Id, seat.Id, ev.Id, $"jti-{Guid.NewGuid():N}");
        db.Events.Add(ev);
        db.AppUsers.Add(new AppUser(userId, "user@test.dev", "User", "pw", UserRole.Attendee));
        db.Seats.Add(seat);
        db.SeatStatuses.Add(status);
        db.Orders.Add(order);
        db.Tickets.Add(ticket);
        db.SaveChanges();
        return ticket;
    }
}
