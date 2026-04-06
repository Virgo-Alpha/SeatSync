using System.Net;
using System.Net.Http.Json;
using SeatSync.Web.Models.Api;

namespace SeatSync.Web.Services;

public sealed class SeatSyncApiClient : ISeatSyncApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SeatSyncApiClient> _logger;

    public SeatSyncApiClient(HttpClient httpClient, ILogger<SeatSyncApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EventApiModel>> GetEventsAsync(CancellationToken ct)
    {
        var events = await _httpClient.GetFromJsonAsync<List<EventApiModel>>("api/events", ct);
        return events ?? [];
    }

    public async Task<IReadOnlyList<SeatApiModel>> GetSeatsAsync(Guid eventId, CancellationToken ct)
    {
        var seats = await _httpClient.GetFromJsonAsync<List<SeatApiModel>>($"api/events/{eventId}/seats", ct);
        return seats ?? [];
    }

    public async Task<bool> CreateHoldAsync(
        Guid eventId,
        Guid userId,
        IReadOnlyCollection<Guid> seatIds,
        CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/holds",
            new CreateHoldRequestApiModel(eventId, userId, seatIds.ToList()),
            ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "CreateHold failed with status {StatusCode}. Body: {Body}",
            (int)response.StatusCode,
            errorBody);

        return false;
    }
}
