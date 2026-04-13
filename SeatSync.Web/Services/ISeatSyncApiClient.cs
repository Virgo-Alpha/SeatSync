using SeatSync.Web.Models.Api;

namespace SeatSync.Web.Services;

public interface ISeatSyncApiClient
{
    Task<LoginResponseApiModel?> LoginAsync(string email, string password, CancellationToken ct);

    Task<IReadOnlyList<EventApiModel>> GetEventsAsync(CancellationToken ct);

    Task<IReadOnlyList<SeatApiModel>> GetSeatsAsync(Guid eventId, CancellationToken ct);

    Task<CreateHoldResultApiModel> CreateHoldAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> seatIds,
        CancellationToken ct);

    Task<FinalizeOrderResultApiModel> FinalizeOrderAsync(
        Guid holdId,
        string idempotencyKey,
        CancellationToken ct);

    Task<EventApiModel?> CreateEventAsync(
        string name,
        DateTimeOffset startsAt,
        string? agenda,
        Guid? copySeatsFromEventId,
        CancellationToken ct);

    Task<MockPaymentResultApiModel> MockPaymentAsync(
        Guid orderId,
        bool shouldSucceed,
        CancellationToken ct);

    Task<string?> DownloadReceiptAsync(Guid orderId, CancellationToken ct);

    Task<bool> EmailReceiptAsync(Guid orderId, string? emailTo, CancellationToken ct);
}
