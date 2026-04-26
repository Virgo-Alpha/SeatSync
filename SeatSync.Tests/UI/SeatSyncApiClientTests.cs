using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SeatSync.Web.Models;
using SeatSync.Web.Services;

namespace SeatSync.Tests.UI;

public class SeatSyncApiClientTests
{
    [Fact]
    public async Task LoginAsync_Should_Return_Null_When_Api_Returns_NonSuccess()
    {
        var sut = BuildClient(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new UserSessionService());

        var result = await sut.LoginAsync("user@test.dev", "bad-password", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEventsAsync_Should_Return_Empty_When_Api_Returns_NonSuccess()
    {
        var sut = BuildClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new UserSessionService());

        var result = await sut.GetEventsAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSeatsAsync_Should_Return_Empty_When_Api_Returns_NonSuccess()
    {
        var sut = BuildClient(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            new UserSessionService());

        var result = await sut.GetSeatsAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateHoldAsync_Should_Send_Bearer_Token_For_Authorized_Call()
    {
        HttpRequestMessage? capturedRequest = null;
        var session = new UserSessionService();
        session.SetUser(new AuthenticatedUserModel(
            Guid.NewGuid(),
            "Demo",
            "demo@test.dev",
            "Attendee",
            "token-123",
            DateTimeOffset.UtcNow.AddMinutes(20)));

        var sut = BuildClient(
            request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"holdId\":\"6f194dc4-5904-47e2-9cf4-57c1697465ef\",\"expiresAt\":\"2030-01-01T00:00:00+00:00\"}")
                };
            },
            session);

        var result = await sut.CreateHoldAsync(Guid.NewGuid(), [Guid.NewGuid()], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().BeEquivalentTo(
            new AuthenticationHeaderValue("Bearer", "token-123"));
    }

    [Fact]
    public async Task FinalizeOrderAsync_Should_Map_409_To_Conflict_Result()
    {
        var sut = BuildClient(
            _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("Hold is no longer active.")
            },
            AuthenticatedSession());

        var result = await sut.FinalizeOrderAsync(
            Guid.NewGuid(),
            $"idem-{Guid.NewGuid():N}",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.Message.Should().Contain("no longer active");
    }

    [Fact]
    public async Task MockPaymentAsync_Should_Map_NonSuccess_To_Failure_Result()
    {
        var sut = BuildClient(
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("payment failure")
            },
            AuthenticatedSession());

        var result = await sut.MockPaymentAsync(Guid.NewGuid(), true, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("payment failure");
    }

    [Fact]
    public async Task EmailReceiptAsync_Should_Return_False_On_NonSuccess()
    {
        var sut = BuildClient(
            _ => new HttpResponseMessage(HttpStatusCode.Forbidden),
            AuthenticatedSession());

        var result = await sut.EmailReceiptAsync(Guid.NewGuid(), "demo@test.dev", CancellationToken.None);

        result.Should().BeFalse();
    }

    private static SeatSyncApiClient BuildClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        IUserSessionService session)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new SeatSyncApiClient(httpClient, session, NullLogger<SeatSyncApiClient>.Instance);
    }

    private static IUserSessionService AuthenticatedSession()
    {
        var session = new UserSessionService();
        session.SetUser(new AuthenticatedUserModel(
            Guid.NewGuid(),
            "Demo",
            "demo@test.dev",
            "Attendee",
            "token-xyz",
            DateTimeOffset.UtcNow.AddMinutes(20)));
        return session;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
