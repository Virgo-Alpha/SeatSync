using System.Security.Claims;
using FluentAssertions;
using SeatSync.Api.Auth;

namespace SeatSync.Tests.Api;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetRequiredUserId_Should_Return_Parsed_Guid()
    {
        var userId = Guid.NewGuid();
        var principal = BuildPrincipal(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ]);

        var parsed = principal.GetRequiredUserId();

        parsed.Should().Be(userId);
    }

    [Fact]
    public void GetRequiredUserId_Should_Throw_When_Missing()
    {
        var principal = BuildPrincipal([]);

        var act = () => principal.GetRequiredUserId();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*User id claim is missing.*");
    }

    [Fact]
    public void GetRequiredDisplayName_Should_Return_Name()
    {
        var principal = BuildPrincipal(
        [
            new Claim(ClaimTypes.Name, "Demo User")
        ]);

        var name = principal.GetRequiredDisplayName();

        name.Should().Be("Demo User");
    }

    [Fact]
    public void GetRequiredDisplayName_Should_Throw_When_Missing()
    {
        var principal = BuildPrincipal([]);

        var act = () => principal.GetRequiredDisplayName();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*User name claim is missing.*");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Organizer")]
    public void IsAdminOrOrganizer_Should_Return_True_For_Expected_Roles(string role)
    {
        var principal = BuildPrincipal(
        [
            new Claim(ClaimTypes.Role, role)
        ]);

        principal.IsAdminOrOrganizer().Should().BeTrue();
    }

    [Fact]
    public void IsAdminOrOrganizer_Should_Return_False_For_Other_Roles()
    {
        var principal = BuildPrincipal(
        [
            new Claim(ClaimTypes.Role, "Attendee")
        ]);

        principal.IsAdminOrOrganizer().Should().BeFalse();
    }

    private static ClaimsPrincipal BuildPrincipal(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
