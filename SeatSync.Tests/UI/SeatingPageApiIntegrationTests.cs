using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SeatSync.Web.Components.Pages;
using SeatSync.Web.Models.Api;
using SeatSync.Web.Services;
using System.Net;
using System.Net.Http;

namespace SeatSync.Tests.UI;

public class SeatingPageApiIntegrationTests : TestContext
{
    [Fact]
    public void Seating_Page_Should_Load_Seat_Map_From_Api()
    {
        var api = new FakeSeatSyncApiClient
        {
            Events = [new EventApiModel(Guid.NewGuid(), "Demo", DateTimeOffset.UtcNow.AddHours(1), null)],
            Seats =
            [
                new SeatApiModel(Guid.NewGuid(), "Orchestra", "A", "1", 1, 1, 0),
                new SeatApiModel(Guid.NewGuid(), "Orchestra", "A", "2", 2, 1, 2)
            ]
        };
        var session = new UserSessionService();
        session.SetUser(new SeatSync.Web.Models.AuthenticatedUserModel(
            Guid.NewGuid(),
            "Demo User",
            "attendee@seatsync.demo",
            "Attendee",
            "fake-token",
            DateTimeOffset.UtcNow.AddHours(2)));

        Services.AddSingleton<IUserSessionService>(session);
        Services.AddSingleton<ISeatSyncApiClient>(api);
        Services.AddSingleton<ICheckoutFlowState>(new CheckoutFlowState());

        var cut = RenderComponent<Seating>(parameters =>
            parameters.Add(p => p.EventId, api.Events[0].Id));

        cut.WaitForAssertion(() => cut.FindAll("button.seat").Should().HaveCount(2));
    }

    [Fact]
    public async Task SeatSyncApiClient_CreateHold_Should_Return_Success_Result_On_200()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"holdId\":\"6f194dc4-5904-47e2-9cf4-57c1697465ef\",\"expiresAt\":\"2030-01-01T00:00:00+00:00\"}")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new SeatSyncApiClient(client, CreateSession(), NullLogger<SeatSyncApiClient>.Instance);

        var result = await api.CreateHoldAsync(
            Guid.NewGuid(),
            [Guid.NewGuid()],
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.IsConflict.Should().BeFalse();
        result.HoldId.Should().NotBeNull();
    }

    [Fact]
    public async Task SeatSyncApiClient_CreateHold_Should_Return_Conflict_Result_On_409()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("One or more seats are not available.")
            });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new SeatSyncApiClient(client, CreateSession(), NullLogger<SeatSyncApiClient>.Instance);

        var result = await api.CreateHoldAsync(
            Guid.NewGuid(),
            [Guid.NewGuid()],
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.Message.Should().Contain("not available");
    }

    [Fact]
    public async Task SeatSyncApiClient_FinalizeOrder_Should_Return_Success_Result_On_200()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"orderId\":\"6f194dc4-5904-47e2-9cf4-57c1697465ef\"}")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new SeatSyncApiClient(client, CreateSession(), NullLogger<SeatSyncApiClient>.Instance);

        var result = await api.FinalizeOrderAsync(
            Guid.NewGuid(),
            $"idem-{Guid.NewGuid():N}",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OrderId.Should().NotBeNull();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class FakeSeatSyncApiClient : ISeatSyncApiClient
    {
        public IReadOnlyList<EventApiModel> Events { get; init; } = [];
        public IReadOnlyList<SeatApiModel> Seats { get; init; } = [];
        public CreateHoldResultApiModel HoldResult { get; init; } = new(true, false, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10), null);
        public FinalizeOrderResultApiModel FinalizeResult { get; init; } = new(true, false, Guid.NewGuid(), null);

        public Task<IReadOnlyList<EventApiModel>> GetEventsAsync(CancellationToken ct) =>
            Task.FromResult(Events);

        public Task<IReadOnlyList<SeatApiModel>> GetSeatsAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult(Seats);

        public Task<LoginResponseApiModel?> LoginAsync(string email, string password, CancellationToken ct) =>
            Task.FromResult<LoginResponseApiModel?>(new LoginResponseApiModel(
                Guid.NewGuid(),
                "Demo User",
                email,
                "Attendee",
                "fake-token",
                DateTimeOffset.UtcNow.AddHours(2)));

        public Task<CreateHoldResultApiModel> CreateHoldAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> seatIds,
            CancellationToken ct) =>
            Task.FromResult(HoldResult);

        public Task<FinalizeOrderResultApiModel> FinalizeOrderAsync(
            Guid holdId,
            string idempotencyKey,
            CancellationToken ct) =>
            Task.FromResult(FinalizeResult);

        public Task<EventApiModel?> CreateEventAsync(
            string name,
            DateTimeOffset startsAt,
            string? agenda,
            Guid? copySeatsFromEventId,
            CancellationToken ct) =>
            Task.FromResult<EventApiModel?>(new EventApiModel(Guid.NewGuid(), name, startsAt, agenda));

        public Task<EventApiModel?> UpdateEventAsync(
            Guid eventId,
            string name,
            DateTimeOffset startsAt,
            string? agenda,
            CancellationToken ct) =>
            Task.FromResult<EventApiModel?>(new EventApiModel(eventId, name, startsAt, agenda));

        public Task<bool> DeleteEventAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<EventReservationApiModel>> GetEventReservationsAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EventReservationApiModel>>([]);

        public Task<bool> CreateSeatsAsync(
            Guid eventId,
            IReadOnlyCollection<CreateSeatRequestApiModel> seats,
            CancellationToken ct) =>
            Task.FromResult(true);

        public Task<MockPaymentResultApiModel> MockPaymentAsync(
            Guid orderId,
            bool shouldSucceed,
            CancellationToken ct) =>
            Task.FromResult(new MockPaymentResultApiModel(true, shouldSucceed ? "Captured" : "Failed", "ok"));

        public Task<string?> DownloadReceiptAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult<string?>("receipt");

        public Task<byte[]?> DownloadReceiptPdfAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult<byte[]?>([1, 2, 3]);

        public Task<bool> EmailReceiptAsync(Guid orderId, string? emailTo, CancellationToken ct) =>
            Task.FromResult(true);
    }

    private static IUserSessionService CreateSession()
    {
        var session = new UserSessionService();
        session.SetUser(new SeatSync.Web.Models.AuthenticatedUserModel(
            Guid.NewGuid(),
            "Demo User",
            "attendee@seatsync.demo",
            "Attendee",
            "fake-token",
            DateTimeOffset.UtcNow.AddHours(2)));

        return session;
    }
}
