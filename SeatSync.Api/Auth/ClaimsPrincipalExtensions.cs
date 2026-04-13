using System.Security.Claims;

namespace SeatSync.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new InvalidOperationException("User id claim is missing.");
    }

    public static string GetRequiredDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? throw new InvalidOperationException("User name claim is missing.");

    public static bool IsAdminOrOrganizer(this ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("Organizer");
}
