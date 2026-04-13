namespace SeatSync.Api.Contracts.Auth;

public sealed record LoginResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc
);
