using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SeatSync.Web.Components.Pages;
using SeatSync.Web.Models;
using SeatSync.Web.Models.Api;
using SeatSync.Web.Services;

namespace SeatSync.Tests.UI;

public class FormSubmitButtonStateTests : TestContext
{
    [Fact]
    public void Login_Submit_Button_Should_Be_Enabled_On_Initial_Render()
    {
        Services.AddSingleton<IUserSessionService>(new UserSessionService());
        Services.AddSingleton<ISeatSyncApiClient>(new FakeSeatSyncApiClient());

        var cut = RenderComponent<Login>();

        var submit = cut.Find("button[type='submit']");
        submit.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void EventManage_Submit_Button_Should_Be_Enabled_When_User_Can_Create_Events()
    {
        var session = new UserSessionService();
        session.SetUser(new AuthenticatedUserModel(
            Guid.NewGuid(),
            "Organizer User",
            "organizer@seatsync.demo",
            "Organizer",
            "fake-token",
            DateTimeOffset.UtcNow.AddHours(1)));

        Services.AddSingleton<IUserSessionService>(session);
        Services.AddSingleton<ISeatSyncApiClient>(new FakeSeatSyncApiClient());

        var cut = RenderComponent<EventManage>();

        var submit = cut.Find("button[type='submit']");
        submit.HasAttribute("disabled").Should().BeFalse();
    }

    private sealed class FakeSeatSyncApiClient : ISeatSyncApiClient
    {
        public Task<LoginResponseApiModel?> LoginAsync(string email, string password, CancellationToken ct) =>
            Task.FromResult<LoginResponseApiModel?>(new LoginResponseApiModel(
                Guid.NewGuid(),
                "Demo User",
                email,
                "Attendee",
                "fake-token",
                DateTimeOffset.UtcNow.AddHours(1)));

        public Task<IReadOnlyList<EventApiModel>> GetEventsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EventApiModel>>([]);

        public Task<IReadOnlyList<SeatApiModel>> GetSeatsAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SeatApiModel>>([]);

        public Task<CreateHoldResultApiModel> CreateHoldAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> seatIds,
            CancellationToken ct) =>
            Task.FromResult(new CreateHoldResultApiModel(true, false, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10), null));

        public Task<FinalizeOrderResultApiModel> FinalizeOrderAsync(
            Guid holdId,
            string idempotencyKey,
            CancellationToken ct) =>
            Task.FromResult(new FinalizeOrderResultApiModel(true, false, Guid.NewGuid(), null));

        public Task<EventApiModel?> CreateEventAsync(
            string name,
            DateTimeOffset startsAt,
            string? agenda,
            Guid? copySeatsFromEventId,
            CancellationToken ct) =>
            Task.FromResult<EventApiModel?>(new EventApiModel(Guid.NewGuid(), name, startsAt, agenda));

        public Task<MockPaymentResultApiModel> MockPaymentAsync(
            Guid orderId,
            bool shouldSucceed,
            CancellationToken ct) =>
            Task.FromResult(new MockPaymentResultApiModel(true, shouldSucceed ? "Captured" : "Failed", "ok"));

        public Task<string?> DownloadReceiptAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult<string?>("receipt");

        public Task<bool> EmailReceiptAsync(Guid orderId, string? emailTo, CancellationToken ct) =>
            Task.FromResult(true);
    }
}
