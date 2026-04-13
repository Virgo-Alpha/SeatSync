namespace SeatSync.Web.Models;

public sealed record AuthenticatedUserModel(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc
);
