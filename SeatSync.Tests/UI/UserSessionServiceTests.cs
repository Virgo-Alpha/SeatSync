using FluentAssertions;
using SeatSync.Web.Models;
using SeatSync.Web.Services;

namespace SeatSync.Tests.UI;

public class UserSessionServiceTests
{
    [Fact]
    public void IsAuthenticated_Should_Be_True_When_User_Not_Expired()
    {
        var sut = new UserSessionService();
        sut.SetUser(BuildUser("Organizer", DateTimeOffset.UtcNow.AddMinutes(10)));

        sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_Should_Be_False_When_User_Expired()
    {
        var sut = new UserSessionService();
        sut.SetUser(BuildUser("Organizer", DateTimeOffset.UtcNow.AddMinutes(-1)));

        sut.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsInRole_Should_Be_Case_Insensitive()
    {
        var sut = new UserSessionService();
        sut.SetUser(BuildUser("Organizer", DateTimeOffset.UtcNow.AddMinutes(10)));

        sut.IsInRole("organizer").Should().BeTrue();
        sut.IsInRole("ADMIN").Should().BeFalse();
    }

    [Fact]
    public void SetUser_And_Clear_Should_Raise_Changed_Event()
    {
        var sut = new UserSessionService();
        var raised = 0;
        sut.Changed += () => raised++;

        sut.SetUser(BuildUser("Attendee", DateTimeOffset.UtcNow.AddMinutes(10)));
        sut.Clear();

        raised.Should().Be(2);
    }

    private static AuthenticatedUserModel BuildUser(string role, DateTimeOffset expiresAtUtc) =>
        new(
            Guid.NewGuid(),
            "Demo",
            "demo@test.dev",
            role,
            "token",
            expiresAtUtc);
}
