using SeatSync.Web.Models;

namespace SeatSync.Web.Services;

public sealed class UserSessionService : IUserSessionService
{
    public event Action? Changed;

    public AuthenticatedUserModel? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null && CurrentUser.ExpiresAtUtc > DateTimeOffset.UtcNow;

    public void SetUser(AuthenticatedUserModel user)
    {
        CurrentUser = user;
        Changed?.Invoke();
    }

    public void Clear()
    {
        CurrentUser = null;
        Changed?.Invoke();
    }

    public bool IsInRole(string role) =>
        CurrentUser is not null &&
        string.Equals(CurrentUser.Role, role, StringComparison.OrdinalIgnoreCase);
}
