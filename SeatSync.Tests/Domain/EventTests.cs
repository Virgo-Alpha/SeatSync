using SeatSync.Domain.Entities;
using FluentAssertions;

namespace SeatSync.Tests.Domain;

public class EventTests
{
    [Fact]
    public void Constructor_Should_Set_Properties_When_Valid()
    {
        var name = "Rock Concert";
        var startsAt = DateTimeOffset.UtcNow.AddDays(7);

        var ev = new Event(name, startsAt);

        ev.Name.Should().Be(name);
        ev.StartsAt.Should().Be(startsAt);
        ev.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_Should_Trim_Name()
    {
        var startsAt = DateTimeOffset.UtcNow.AddDays(1);

        var ev = new Event("  Festival  ", startsAt);

        ev.Name.Should().Be("Festival");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Name_Is_Empty()
    {
        var act = () => new Event("", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Event name is required*");
    }

    [Fact]
    public void AddSeat_Should_Add_Seat_To_Collection()
    {
        var ev = new Event("Test Event", DateTimeOffset.UtcNow);

        var seat = new Seat(
            ev.Id,
            "VIP",
            "A",
            "1",
            10m,
            20m);

        ev.AddSeat(seat);

        ev.Seats.Should().ContainSingle();
        ev.Seats.First().Should().Be(seat);
    }
}