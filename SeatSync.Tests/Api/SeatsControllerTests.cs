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

public class SeatsControllerTests
{
    [Fact]
    public async Task GetSeats_Should_Return_Joined_Seats_And_Statuses()
    {
        await using var db = CreateDbContext();
        var ev = new Event("Demo Event", DateTimeOffset.UtcNow.AddDays(2));
        var seat = new Seat(ev.Id, "Orchestra", "A", "1", 1, 2);
        db.Events.Add(ev);
        db.Seats.Add(seat);
        db.SeatStatuses.Add(new SeatStatus(ev.Id, seat.Id, SeatState.Held));
        await db.SaveChangesAsync();

        var sut = new SeatsController(db);

        var result = await sut.GetSeats(ev.Id, CancellationToken.None);
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();

        payload.Should().HaveCount(1);
    }

    [Fact]
    public async Task BulkCreate_Should_Return_404_When_Event_Not_Found()
    {
        await using var db = CreateDbContext();
        var sut = new SeatsController(db);

        var response = await sut.BulkCreate(
            Guid.NewGuid(),
            [new SeatCreateRequest("Main", "A", "1", 1, 1)],
            CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task BulkCreate_Should_Create_Seats_And_Statuses()
    {
        await using var db = CreateDbContext();
        var ev = new Event("Demo Event", DateTimeOffset.UtcNow.AddDays(2));
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var sut = new SeatsController(db);
        var response = await sut.BulkCreate(
            ev.Id,
            [
                new SeatCreateRequest("Main", "A", "1", 1, 1),
                new SeatCreateRequest("Main", "A", "2", 2, 1)
            ],
            CancellationToken.None);

        response.Should().BeOfType<OkResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
        (await db.Seats.CountAsync(x => x.EventId == ev.Id)).Should().Be(2);
        (await db.SeatStatuses.CountAsync(x => x.EventId == ev.Id && x.State == SeatState.Available)).Should().Be(2);
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
}
