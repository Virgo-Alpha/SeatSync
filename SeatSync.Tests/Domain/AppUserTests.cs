using FluentAssertions;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Tests.Domain;

public class AppUserTests
{
    [Fact]
    public void Constructor_Should_Normalize_Email_And_Name()
    {
        var user = new AppUser(
            Guid.NewGuid(),
            "  User@Test.Dev  ",
            "  Demo User  ",
            "pw",
            UserRole.Attendee);

        user.Email.Should().Be("user@test.dev");
        user.DisplayName.Should().Be("Demo User");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Should_Throw_When_Email_Invalid(string email)
    {
        var act = () => new AppUser(Guid.NewGuid(), email, "Demo", "pw", UserRole.Attendee);
        act.Should().Throw<ArgumentException>().WithMessage("*Email is required*");
    }

    [Fact]
    public void VerifyPassword_Should_Be_Ordinal_And_Exact()
    {
        var user = new AppUser(Guid.NewGuid(), "user@test.dev", "Demo", "Pass123", UserRole.Attendee);

        user.VerifyPassword("Pass123").Should().BeTrue();
        user.VerifyPassword("pass123").Should().BeFalse();
    }
}
