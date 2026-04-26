using FluentAssertions;
using SeatSync.Domain.Entities;

namespace SeatSync.Tests.Domain;

public class SeatTests
{
    [Fact]
    public void Constructor_Should_Throw_When_EventId_Empty()
    {
        var act = () => new Seat(Guid.Empty, "Main", "A", "1", 1, 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*EventId cannot be empty*");
    }

    [Fact]
    public void Constructor_Should_Trim_Fields()
    {
        var seat = new Seat(Guid.NewGuid(), " Main ", " A ", " 10 ", 1, 2);

        seat.Section.Should().Be("Main");
        seat.Row.Should().Be("A");
        seat.Number.Should().Be("10");
    }

    [Fact]
    public void SetPosition_Should_Update_Coordinates()
    {
        var seat = new Seat(Guid.NewGuid(), "Main", "A", "1");

        seat.SetPosition(10, 20);

        seat.X.Should().Be(10);
        seat.Y.Should().Be(20);
    }

    [Fact]
    public void GetDisplayLabel_Should_Combine_Section_Row_Number()
    {
        var seat = new Seat(Guid.NewGuid(), "VIP", "B", "4");

        seat.GetDisplayLabel().Should().Be("VIP-B-4");
    }
}
