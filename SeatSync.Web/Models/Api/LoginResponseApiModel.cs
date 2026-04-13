namespace SeatSync.Web.Models.Api;

public sealed record LoginResponseApiModel(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc
);
