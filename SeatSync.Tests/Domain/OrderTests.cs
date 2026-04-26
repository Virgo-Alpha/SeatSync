using FluentAssertions;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Tests.Domain;

public class OrderTests
{
    [Fact]
    public void Constructor_Should_Throw_When_Amount_Not_Positive()
    {
        var act = () => new Order(Guid.NewGuid(), Guid.NewGuid(), 0m, "USD", "idem-1", [Guid.NewGuid()]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Amount must be positive*");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Idempotency_Key_Missing()
    {
        var act = () => new Order(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", "", [Guid.NewGuid()]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Idempotency key required*");
    }

    [Fact]
    public void MarkAuthorized_Should_Throw_When_Not_Pending()
    {
        var order = NewOrder();
        order.MarkAuthorized();

        var act = () => order.MarkAuthorized();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void MarkCaptured_Should_Set_CompletedAt()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        var now = DateTimeOffset.UtcNow;

        order.MarkCaptured(new FixedClock(now));

        order.State.Should().Be(PaymentState.Captured);
        order.CompletedAt.Should().Be(now);
    }

    [Fact]
    public void MarkCaptured_Should_Throw_When_Not_Authorized()
    {
        var order = NewOrder();

        var act = () => order.MarkCaptured(TimeProvider.System);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*authorized*");
    }

    [Fact]
    public void MarkFailed_Should_Throw_When_Finalized()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        order.MarkCaptured(TimeProvider.System);

        var act = () => order.MarkFailed();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*finalized state*");
    }

    [Fact]
    public void MarkRefunded_Should_Throw_When_Not_Captured()
    {
        var order = NewOrder();

        var act = () => order.MarkRefunded();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*hasn't been captured*");
    }

    [Fact]
    public void MarkRefunded_Should_Succeed_When_Captured()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        order.MarkCaptured(TimeProvider.System);

        order.MarkRefunded();

        order.State.Should().Be(PaymentState.Refunded);
    }

    private static Order NewOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", $"idem-{Guid.NewGuid():N}", [Guid.NewGuid()]);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
