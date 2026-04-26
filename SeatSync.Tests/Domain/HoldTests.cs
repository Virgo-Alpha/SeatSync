using FluentAssertions;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Tests.Domain;

public class HoldTests
{
    [Fact]
    public void Constructor_Should_Throw_When_SeatIds_Empty()
    {
        var act = () => new Hold(Guid.NewGuid(), Guid.NewGuid(), [], TimeSpan.FromMinutes(10));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one seat*");
    }

    [Fact]
    public void IsExpired_Should_Return_True_When_Clock_Past_Expiry()
    {
        var hold = new Hold(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], TimeSpan.FromMinutes(1));
        var clock = new FakeTimeProvider(hold.ExpiresAt.AddSeconds(1));

        hold.IsExpired(clock).Should().BeTrue();
    }

    [Fact]
    public void ValidateOwnership_Should_Throw_For_Different_User()
    {
        var hold = new Hold(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], TimeSpan.FromMinutes(5));

        var act = () => hold.ValidateOwnership(Guid.NewGuid());

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void MarkConverted_Should_Throw_When_Not_Active()
    {
        var hold = new Hold(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], TimeSpan.FromMinutes(5));
        hold.MarkExpired();

        var act = () => hold.MarkConverted();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public void Cancel_Should_Throw_When_Already_Converted()
    {
        var hold = new Hold(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], TimeSpan.FromMinutes(5));
        hold.MarkConverted();

        var act = () => hold.Cancel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been converted*");
    }

    [Fact]
    public void MarkExpired_Should_Set_State()
    {
        var hold = new Hold(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], TimeSpan.FromMinutes(5));

        hold.MarkExpired();

        hold.State.Should().Be(HoldState.Expired);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
