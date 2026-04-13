using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SeatSync.Api.Auth;
using SeatSync.Api.Contracts.Auth;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly SeatSyncDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(
        SeatSyncDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, ct);

        if (user is null || !user.VerifyPassword(request.Password))
        {
            return Unauthorized("Invalid email or password.");
        }

        var token = _tokenService.CreateAccessToken(user);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        return Ok(new LoginResponse(
            user.Id,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            token,
            expiresAt));
    }
}
