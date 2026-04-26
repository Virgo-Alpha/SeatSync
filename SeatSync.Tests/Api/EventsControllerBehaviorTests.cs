using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatSync.Api.Controllers;
using SeatSync.Api.Contracts.Events;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Tests.Api;

public class EventsControllerBehaviorTests
{
    [Fact]
    public async Task GenerateSeatInventory_Should_Return_404_When_Target_Missing()
    {
        await using var db = CreateDbContext();
        var sut = NewController(db);

        var response = await sut.GenerateSeatInventory(
            Guid.NewGuid(),
            new GenerateSeatInventoryRequest(Guid.NewGuid()),
            CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GenerateSeatInventory_Should_Return_404_When_Source_Missing()
    {
        await using var db = CreateDbContext();
        var target = new Event("Target", DateTimeOffset.UtcNow.AddDays(1));
        db.Events.Add(target);
        await db.SaveChangesAsync();

        var sut = NewController(db);
        var response = await sut.GenerateSeatInventory(
            target.Id,
            new GenerateSeatInventoryRequest(Guid.NewGuid()),
            CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GenerateSeatInventory_Should_Return_Ok_When_Already_Generated()
    {
        await using var db = CreateDbContext();
        var target = new Event("Target", DateTimeOffset.UtcNow.AddDays(1));
        db.Events.Add(target);
        db.Seats.Add(new Seat(target.Id, "Main", "A", "1", 1, 1));
        await db.SaveChangesAsync();

        var sut = NewController(db);
        var response = await sut.GenerateSeatInventory(
            target.Id,
            new GenerateSeatInventoryRequest(target.Id),
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GenerateSeatInventory_Should_Return_BadRequest_When_Source_Equals_Target()
    {
        await using var db = CreateDbContext();
        var ev = new Event("Same", DateTimeOffset.UtcNow.AddDays(1));
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var sut = NewController(db);
        var response = await sut.GenerateSeatInventory(
            ev.Id,
            new GenerateSeatInventoryRequest(ev.Id),
            CancellationToken.None);

        response.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GenerateSeatInventory_Should_Return_BadRequest_When_Source_Has_No_Seats()
    {
        await using var db = CreateDbContext();
        var source = new Event("Source", DateTimeOffset.UtcNow.AddDays(1));
        var target = new Event("Target", DateTimeOffset.UtcNow.AddDays(1));
        db.Events.AddRange(source, target);
        await db.SaveChangesAsync();

        var sut = NewController(db);
        var response = await sut.GenerateSeatInventory(
            target.Id,
            new GenerateSeatInventoryRequest(source.Id),
            CancellationToken.None);

        response.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GenerateSeatInventory_Should_Copy_Seats_On_Success()
    {
        await using var db = CreateDbContext();
        var source = new Event("Source", DateTimeOffset.UtcNow.AddDays(1));
        var target = new Event("Target", DateTimeOffset.UtcNow.AddDays(1));
        var sourceSeat = new Seat(source.Id, "Main", "A", "1", 1, 1);
        db.Events.AddRange(source, target);
        db.Seats.Add(sourceSeat);
        db.SeatStatuses.Add(new SeatStatus(source.Id, sourceSeat.Id, SeatState.Available));
        await db.SaveChangesAsync();

        var sut = NewController(db);
        var response = await sut.GenerateSeatInventory(
            target.Id,
            new GenerateSeatInventoryRequest(source.Id),
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        (await db.Seats.CountAsync(x => x.EventId == target.Id)).Should().Be(1);
        (await db.SeatStatuses.CountAsync(x => x.EventId == target.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Create_Should_Return_BadRequest_When_Copy_Source_Has_No_Seats()
    {
        await using var db = CreateDbContext();
        var source = new Event("Source", DateTimeOffset.UtcNow.AddDays(1));
        db.Events.Add(source);
        await db.SaveChangesAsync();

        var sut = NewController(db);
        var response = await sut.Create(
            new CreateEventRequest("Target", DateTimeOffset.UtcNow.AddDays(2), null, source.Id),
            CancellationToken.None);

        response.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_Should_Return_NotFound_When_Event_Missing()
    {
        await using var db = CreateDbContext();
        var sut = NewController(db);

        var response = await sut.Delete(Guid.NewGuid(), CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_Should_Remove_Event_When_Found()
    {
        await using var db = CreateDbContext();
        var ev = new Event("Delete", DateTimeOffset.UtcNow.AddDays(1));
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        var sut = NewController(db);

        var response = await sut.Delete(ev.Id, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        (await db.Events.AnyAsync(x => x.Id == ev.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetReservations_Should_Return_NotFound_When_Event_Missing()
    {
        await using var db = CreateDbContext();
        var sut = NewController(db);

        var response = await sut.GetReservations(Guid.NewGuid(), CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static EventsController NewController(SeatSyncDbContext db)
    {
        var controller = new EventsController(db);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Organizer User"),
            new Claim(ClaimTypes.Role, "Organizer")
        ], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
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
