using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SeatSync.Api.Auth;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Tests.Api;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_Should_Include_Expected_Claims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "issuer.test",
            Audience = "aud.test",
            SigningKey = "this-is-a-test-signing-key-with-enough-length",
            ExpiryMinutes = 120
        });
        var sut = new JwtTokenService(options);
        var user = new AppUser(Guid.NewGuid(), "user@test.dev", "Demo User", "pw", UserRole.Organizer);

        var token = sut.CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("issuer.test");
        jwt.Audiences.Should().ContainSingle("aud.test");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "user@test.dev");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "Demo User");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == UserRole.Organizer.ToString());
    }

    [Fact]
    public void CreateAccessToken_Should_Set_Expiry_Close_To_Configured_Window()
    {
        var options = Options.Create(new JwtOptions
        {
            SigningKey = "this-is-a-test-signing-key-with-enough-length",
            ExpiryMinutes = 5
        });
        var sut = new JwtTokenService(options);
        var user = new AppUser(Guid.NewGuid(), "user@test.dev", "Demo User", "pw", UserRole.Attendee);

        var before = DateTime.UtcNow;
        var token = sut.CreateAccessToken(user);
        var after = DateTime.UtcNow;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.ValidTo.Should().BeOnOrAfter(before.AddMinutes(4));
        jwt.ValidTo.Should().BeOnOrBefore(after.AddMinutes(6));
    }
}
