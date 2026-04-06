using SeatSync.Web.Models.Api;

namespace SeatSync.Web.Services;

public interface ISeatSyncApiClient
{
    Task<IReadOnlyList<EventApiModel>> GetEventsAsync(CancellationToken ct);

    Task<IReadOnlyList<SeatApiModel>> GetSeatsAsync(Guid eventId, CancellationToken ct);

    Task<bool> CreateHoldAsync(
        Guid eventId,
        Guid userId,
        IReadOnlyCollection<Guid> seatIds,
        CancellationToken ct);
}
