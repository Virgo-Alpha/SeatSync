using SeatSync.Domain.Entities;

namespace SeatSync.Api.Auth;

public interface ITokenService
{
    string CreateAccessToken(AppUser user);
}
