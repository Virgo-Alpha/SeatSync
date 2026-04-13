using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SeatSync.Web.Models.Api;

namespace SeatSync.Web.Services;

public sealed class SeatSyncApiClient : ISeatSyncApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _session;
    private readonly ILogger<SeatSyncApiClient> _logger;

    public SeatSyncApiClient(
        HttpClient httpClient,
        IUserSessionService session,
        ILogger<SeatSyncApiClient> logger)
    {
        _httpClient = httpClient;
        _session = session;
        _logger = logger;
    }

    public async Task<LoginResponseApiModel?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequestApiModel(email, password),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponseApiModel>(JsonOptions, ct);
    }

    public async Task<IReadOnlyList<EventApiModel>> GetEventsAsync(CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Get, "api/events", requiresAuth: false);
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var events = await response.Content.ReadFromJsonAsync<List<EventApiModel>>(JsonOptions, ct);
        return events ?? [];
    }

    public async Task<IReadOnlyList<SeatApiModel>> GetSeatsAsync(Guid eventId, CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Get, $"api/events/{eventId}/seats", requiresAuth: false);
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var seats = await response.Content.ReadFromJsonAsync<List<SeatApiModel>>(JsonOptions, ct);
        return seats ?? [];
    }

    public async Task<CreateHoldResultApiModel> CreateHoldAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> seatIds,
        CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Post, "api/holds", requiresAuth: true);
        request.Content = JsonContent.Create(new CreateHoldRequestApiModel(eventId, seatIds.ToList()));

        var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<CreateHoldResponseApiModel>(JsonOptions, ct);
            return new CreateHoldResultApiModel(true, false, payload?.HoldId, payload?.ExpiresAt, null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflictBody = await response.Content.ReadAsStringAsync(ct);
            return new CreateHoldResultApiModel(false, true, null, null, ToMessage(conflictBody));
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("CreateHold failed with status {StatusCode}. Body: {Body}", (int)response.StatusCode, errorBody);
        return new CreateHoldResultApiModel(false, false, null, null, ToMessage(errorBody));
    }

    public async Task<FinalizeOrderResultApiModel> FinalizeOrderAsync(
        Guid holdId,
        string idempotencyKey,
        CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Post, "api/orders/finalize", requiresAuth: true);
        request.Content = JsonContent.Create(new FinalizeOrderRequestApiModel(holdId, idempotencyKey));

        var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<FinalizeOrderSuccessPayload>(JsonOptions, ct);
            return new FinalizeOrderResultApiModel(true, false, payload?.OrderId, null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflictBody = await response.Content.ReadAsStringAsync(ct);
            return new FinalizeOrderResultApiModel(false, true, null, ToMessage(conflictBody));
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("FinalizeOrder failed with status {StatusCode}. Body: {Body}", (int)response.StatusCode, errorBody);
        return new FinalizeOrderResultApiModel(false, false, null, ToMessage(errorBody));
    }

    public async Task<EventApiModel?> CreateEventAsync(
        string name,
        DateTimeOffset startsAt,
        string? agenda,
        Guid? copySeatsFromEventId,
        CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Post, "api/events", requiresAuth: true);
        request.Content = JsonContent.Create(new CreateEventRequestApiModel(name, startsAt, agenda, copySeatsFromEventId));

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<EventApiModel>(JsonOptions, ct);
    }

    public async Task<MockPaymentResultApiModel> MockPaymentAsync(Guid orderId, bool shouldSucceed, CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Post, $"api/orders/{orderId}/payments/mock", requiresAuth: true);
        request.Content = JsonContent.Create(new MockPaymentRequestApiModel(shouldSucceed));

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new MockPaymentResultApiModel(false, null, ToMessage(body) ?? "Mock payment failed.");
        }

        var payload = JsonSerializer.Deserialize<MockPaymentPayload>(body, JsonOptions);
        return new MockPaymentResultApiModel(true, payload?.State, payload?.Message);
    }

    public async Task<string?> DownloadReceiptAsync(Guid orderId, CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Get, $"api/orders/{orderId}/receipt", requiresAuth: true);
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<bool> EmailReceiptAsync(Guid orderId, string? emailTo, CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Post, $"api/orders/{orderId}/receipt/email", requiresAuth: true);
        request.Content = JsonContent.Create(new EmailReceiptRequestApiModel(emailTo));

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, bool requiresAuth)
    {
        var request = new HttpRequestMessage(method, uri);
        if (requiresAuth && _session.CurrentUser is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.CurrentUser.AccessToken);
        }

        return request;
    }

    private static string? ToMessage(string body) =>
        string.IsNullOrWhiteSpace(body) ? null : body;

    private sealed record FinalizeOrderSuccessPayload(Guid? OrderId);
    private sealed record MockPaymentPayload(string? State, string? Message);
}
