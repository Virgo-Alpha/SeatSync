using FluentAssertions;
using SeatSync.Web.Models;

namespace SeatSync.Tests.UI;

public class SeatStateRulesTests
{
    [Theory]
    [InlineData(SeatVisualState.Available, "seat--available")]
    [InlineData(SeatVisualState.Selected, "seat--selected")]
    [InlineData(SeatVisualState.Held, "seat--held")]
    [InlineData(SeatVisualState.Booked, "seat--booked")]
    public void ToCssClass_Should_Return_Expected_Class(SeatVisualState state, string expectedClass)
    {
        var cssClass = SeatStateRules.ToCssClass(state);

        cssClass.Should().Be(expectedClass);
    }

    [Fact]
    public void TryToggle_Should_Set_Available_To_Selected()
    {
        var seat = BuildSeat(SeatVisualState.Available);

        var toggled = SeatStateRules.TryToggle(seat);

        toggled.Should().BeTrue();
        seat.State.Should().Be(SeatVisualState.Selected);
    }

    [Fact]
    public void TryToggle_Should_Set_Selected_To_Available()
    {
        var seat = BuildSeat(SeatVisualState.Selected);

        var toggled = SeatStateRules.TryToggle(seat);

        toggled.Should().BeTrue();
        seat.State.Should().Be(SeatVisualState.Available);
    }

    [Theory]
    [InlineData(SeatVisualState.Held)]
    [InlineData(SeatVisualState.Booked)]
    public void TryToggle_Should_Not_Change_NonInteractive_States(SeatVisualState state)
    {
        var seat = BuildSeat(state);

        var toggled = SeatStateRules.TryToggle(seat);

        toggled.Should().BeFalse();
        seat.State.Should().Be(state);
    }

    private static SeatViewModel BuildSeat(SeatVisualState state) => new()
    {
        Section = "Orchestra",
        Row = "A",
        Number = "1",
        State = state
    };
}
