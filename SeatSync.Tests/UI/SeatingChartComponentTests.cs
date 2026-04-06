using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using SeatSync.Web.Components.Seating;
using SeatSync.Web.Models;

namespace SeatSync.Tests.UI;

public class SeatingChartComponentTests : TestContext
{
    [Fact]
    public void SeatingChart_Should_Render_A_Seat_Button_For_Each_Seat()
    {
        var seats = BuildSeatMap();

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters.Add(p => p.Seats, seats));

        var renderedSeats = cut.FindAll("button.seat");
        renderedSeats.Should().HaveCount(seats.Count);
    }

    [Fact]
    public void Clicking_Available_Seat_Should_Set_It_To_Selected()
    {
        var seat = BuildSeat("A", "1", SeatVisualState.Available);

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters.Add(p => p.Seats, [seat]));

        cut.Find("button.seat").Click();

        seat.State.Should().Be(SeatVisualState.Selected);
        cut.Find("button.seat").ClassList.Should().Contain("seat--selected");
    }

    [Fact]
    public void Clicking_Selected_Seat_Should_Set_It_Back_To_Available()
    {
        var seat = BuildSeat("A", "1", SeatVisualState.Selected);

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters.Add(p => p.Seats, [seat]));

        cut.Find("button.seat").Click();

        seat.State.Should().Be(SeatVisualState.Available);
        cut.Find("button.seat").ClassList.Should().Contain("seat--available");
    }

    [Fact]
    public void Held_And_Booked_Seats_Should_Be_Disabled_And_Not_Toggle()
    {
        var heldSeat = BuildSeat("A", "1", SeatVisualState.Held);
        var bookedSeat = BuildSeat("A", "2", SeatVisualState.Booked);

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters.Add(p => p.Seats, [heldSeat, bookedSeat]));

        var buttons = cut.FindAll("button.seat");
        buttons[0].HasAttribute("disabled").Should().BeTrue();
        buttons[1].HasAttribute("disabled").Should().BeTrue();

        heldSeat.State.Should().Be(SeatVisualState.Held);
        bookedSeat.State.Should().Be(SeatVisualState.Booked);
    }

    [Fact]
    public void Clicking_A_Seat_Should_Invoke_SeatsChanged_Callback()
    {
        var seat = BuildSeat("A", "1", SeatVisualState.Available);
        var callbackInvoked = false;

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters
                .Add(p => p.Seats, [seat])
                .Add(
                    p => p.SeatsChanged,
                    EventCallback.Factory.Create<IReadOnlyList<SeatViewModel>>(
                        this,
                        _ => callbackInvoked = true)));

        cut.Find("button.seat").Click();

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void Seats_Should_Render_In_Numeric_Order_When_SeatNumbers_Are_Strings()
    {
        var seats = new List<SeatViewModel>
        {
            BuildSeat("A", "1", SeatVisualState.Available),
            BuildSeat("A", "10", SeatVisualState.Available),
            BuildSeat("A", "12", SeatVisualState.Available),
            BuildSeat("A", "2", SeatVisualState.Available)
        };

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters.Add(p => p.Seats, seats));

        var labels = cut.FindAll("button.seat").Select(x => x.TextContent.Trim()).ToList();
        labels.Should().Equal("1", "2", "10", "12");
    }

    [Fact]
    public void SeatingChart_Should_Render_Aisle_For_Each_Row()
    {
        var seats = BuildSeatMap();

        var cut = RenderComponent<SeatingChart>(parameters =>
            parameters.Add(p => p.Seats, seats));

        cut.FindAll(".aisle").Should().HaveCount(2);
    }

    private static List<SeatViewModel> BuildSeatMap() =>
    [
        BuildSeat("A", "1", SeatVisualState.Available),
        BuildSeat("A", "2", SeatVisualState.Selected),
        BuildSeat("A", "3", SeatVisualState.Held),
        BuildSeat("B", "1", SeatVisualState.Booked)
    ];

    private static SeatViewModel BuildSeat(string row, string number, SeatVisualState state) => new()
    {
        Section = "Orchestra",
        Row = row,
        Number = number,
        State = state
    };
}
