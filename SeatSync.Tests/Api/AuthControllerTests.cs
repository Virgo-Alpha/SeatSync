using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SeatSync.Api.Auth;
using SeatSync.Api.Contracts.Auth;
using SeatSync.Api.Controllers;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Tests.Api;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_Should_Return_401_When_User_Does_Not_Exist()
    {
        await using var db = CreateDbContext();
        var sut = BuildController(db);

        var response = await sut.Login(
            new LoginRequest("missing@test.dev", "bad"),
            CancellationToken.None);

        response.Result.Should().BeOfType<UnauthorizedObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return_200_With_Token_When_Credentials_Are_Valid()
    {
        await using var db = CreateDbContext();
        var user = new AppUser(
            Guid.NewGuid(),
            "organizer@test.dev",
            "Organizer",
            "demo123",
            UserRole.Organizer);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        var sut = BuildController(db);
        var response = await sut.Login(
            new LoginRequest("  Organizer@Test.Dev  ", "demo123"),
            CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>()
            .Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var payload = ok.Value.Should().BeOfType<LoginResponse>().Subject;
        payload.Email.Should().Be("organizer@test.dev");
        payload.AccessToken.Should().Be("test-token");
    }

    private static AuthController BuildController(SeatSyncDbContext db)
    {
        var tokenService = new FakeTokenService();
        var options = Options.Create(new JwtOptions { ExpiryMinutes = 30 });
        return new AuthController(db, tokenService, options);
    }

    private static SeatSyncDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SeatSyncDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SeatSyncDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateAccessToken(AppUser user) => "test-token";
    }
}
