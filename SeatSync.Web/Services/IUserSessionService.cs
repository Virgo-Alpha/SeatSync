using SeatSync.Web.Models;

namespace SeatSync.Web.Services;

public interface IUserSessionService
{
    event Action? Changed;
    AuthenticatedUserModel? CurrentUser { get; }
    bool IsAuthenticated { get; }

    void SetUser(AuthenticatedUserModel user);
    void Clear();
    bool IsInRole(string role);
}
