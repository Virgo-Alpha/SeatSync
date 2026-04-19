using FluentAssertions;
using SeatSync.Web.Models;
using SeatSync.Web.Services;

namespace SeatSync.Tests.UI;

public class CheckoutFlowStateTests
{
    [Fact]
    public void Set_And_TryGet_Should_Return_Checkout_For_Matching_Hold()
    {
        var state = new CheckoutFlowState();
        var holdId = Guid.NewGuid();
        var checkout = new PendingCheckoutModel(
            holdId,
            Guid.NewGuid(),
            "Demo Event",
            DateTimeOffset.UtcNow.AddDays(1),
            ["A1", "A2"],
            DateTimeOffset.UtcNow.AddMinutes(10));

        state.Set(checkout);

        var found = state.TryGet(holdId, out var resolved);

        found.Should().BeTrue();
        resolved.Should().Be(checkout);
    }

    [Fact]
    public void Clear_Should_Remove_Current_Checkout()
    {
        var state = new CheckoutFlowState();
        state.Set(new PendingCheckoutModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Demo Event",
            DateTimeOffset.UtcNow.AddDays(1),
            ["B1"],
            DateTimeOffset.UtcNow.AddMinutes(10)));

        state.Clear();

        state.Current.Should().BeNull();
    }
}
