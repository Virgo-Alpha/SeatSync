using FluentAssertions;
using SeatSync.Domain.Entities;

namespace SeatSync.Tests.Domain;

public class TicketTests
{
    [Fact]
    public void Constructor_Should_Throw_When_OrderId_Empty()
    {
        var act = () => new Ticket(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "jti-1");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrderId required*");
    }

    [Fact]
    public void Redeem_Should_Set_RedeemedAt()
    {
        var ticket = new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "jti-1");
        var ts = DateTimeOffset.UtcNow;

        ticket.Redeem(ts);

        ticket.IsRedeemed().Should().BeTrue();
        ticket.RedeemedAt.Should().Be(ts);
    }

    [Fact]
    public void Redeem_Should_Throw_If_Already_Redeemed()
    {
        var ticket = new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "jti-1");
        ticket.Redeem(DateTimeOffset.UtcNow);

        var act = () => ticket.Redeem(DateTimeOffset.UtcNow.AddMinutes(1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already redeemed*");
    }
}
