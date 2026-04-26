using FluentAssertions;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Tests.Domain;

public class SeatStatusTests
{
    [Fact]
    public void MarkHeld_Should_Set_Hold_State()
    {
        var status = new SeatStatus(Guid.NewGuid(), Guid.NewGuid(), SeatState.Available);
        var holdId = Guid.NewGuid();

        status.MarkHeld(holdId);

        status.State.Should().Be(SeatState.Held);
        status.HoldId.Should().Be(holdId);
        status.OrderId.Should().BeNull();
    }

    [Fact]
    public void MarkHeld_Should_Throw_When_Not_Available()
    {
        var status = new SeatStatus(Guid.NewGuid(), Guid.NewGuid(), SeatState.Held);

        var act = () => status.MarkHeld(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public void MarkSold_Should_Throw_When_Not_Held()
    {
        var status = new SeatStatus(Guid.NewGuid(), Guid.NewGuid(), SeatState.Available);

        var act = () => status.MarkSold(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be held*");
    }

    [Fact]
    public void Release_Should_Throw_When_Not_Held()
    {
        var status = new SeatStatus(Guid.NewGuid(), Guid.NewGuid(), SeatState.Available);

        var act = () => status.Release();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only held seats*");
    }

    [Fact]
    public void Release_Should_Reset_State()
    {
        var status = new SeatStatus(Guid.NewGuid(), Guid.NewGuid(), SeatState.Available);
        status.MarkHeld(Guid.NewGuid());

        status.Release();

        status.State.Should().Be(SeatState.Available);
        status.HoldId.Should().BeNull();
        status.OrderId.Should().BeNull();
    }
}
