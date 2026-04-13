namespace SeatSync.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "SeatSync.Api";
    public string Audience { get; init; } = "SeatSync.Web";
    public string SigningKey { get; init; } = "SeatSync-super-secret-signing-key-change-me";
    public int ExpiryMinutes { get; init; } = 240;
}
