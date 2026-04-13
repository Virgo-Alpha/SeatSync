using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SeatSync.Api.Contracts.Auth;
using SeatSync.Api.Contracts.Events;
using SeatSync.Web.Models.Api;
using SeatSync.Tests.Infrastructure;

namespace SeatSync.Tests.Api;

public class EventsControllerTests 
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EventsControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_Event_Should_Return_201_And_Location()
    {
        await AuthenticateAsOrganizerAsync();

        var request = new CreateEventRequest(
            "Integration Test Event",
            DateTimeOffset.UtcNow.AddDays(5));

        var response = await _client.PostAsJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content
            .ReadFromJsonAsync<EventResponse>();

        created.Should().NotBeNull();
        created!.Name.Should().Be("Integration Test Event");
    }

    [Fact]
    public async Task GetById_Should_Return_404_When_Not_Exists()
    {
        var response = await _client.GetAsync(
            $"/api/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_Should_Return_Event_When_Exists()
    {
        await AuthenticateAsOrganizerAsync();

        // First create event
        var request = new CreateEventRequest(
            "Fetch Test Event",
            DateTimeOffset.UtcNow.AddDays(3));

        var createResponse = await _client
            .PostAsJsonAsync("/api/events", request);

        var created = await createResponse.Content
            .ReadFromJsonAsync<EventResponse>();

        // Then fetch
        var response = await _client
            .GetAsync($"/api/events/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await response.Content
            .ReadFromJsonAsync<EventResponse>();

        fetched!.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Fetch Test Event");
    }

    [Fact]
    public async Task GenerateSeatInventory_Should_Copy_Seats_From_Source_Event_And_Be_Idempotent()
    {
        await AuthenticateAsOrganizerAsync();

        var sourceEventResponse = await _client.PostAsJsonAsync(
            "/api/events",
            new CreateEventRequest("Source Event", DateTimeOffset.UtcNow.AddDays(7)));
        sourceEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var sourceEvent = await sourceEventResponse.Content.ReadFromJsonAsync<EventResponse>();
        sourceEvent.Should().NotBeNull();

        var createSeatsResponse = await _client.PostAsJsonAsync(
            $"/api/events/{sourceEvent!.Id}/seats",
            new[]
            {
                new { Section = "Orchestra", Row = "A", Number = "1", X = 1m, Y = 1m },
                new { Section = "Orchestra", Row = "A", Number = "2", X = 2m, Y = 1m }
            });
        createSeatsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var targetEventResponse = await _client.PostAsJsonAsync(
            "/api/events",
            new CreateEventRequest("Target Event", DateTimeOffset.UtcNow.AddDays(8)));
        targetEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var targetEvent = await targetEventResponse.Content.ReadFromJsonAsync<EventResponse>();
        targetEvent.Should().NotBeNull();

        var generateResponse = await _client.PostAsJsonAsync(
            $"/api/events/{targetEvent!.Id}/seat-inventory/generate",
            new { SourceEventId = sourceEvent.Id });
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var targetSeats = await _client.GetFromJsonAsync<List<SeatApiModel>>(
            $"/api/events/{targetEvent.Id}/seats");
        targetSeats.Should().NotBeNull();
        targetSeats.Should().HaveCount(2);
        targetSeats!.All(s => s.State == 0).Should().BeTrue();

        var generateAgainResponse = await _client.PostAsJsonAsync(
            $"/api/events/{targetEvent.Id}/seat-inventory/generate",
            new { SourceEventId = sourceEvent.Id });
        generateAgainResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var targetSeatsAfterSecondRun = await _client.GetFromJsonAsync<List<SeatApiModel>>(
            $"/api/events/{targetEvent.Id}/seats");
        targetSeatsAfterSecondRun.Should().HaveCount(2);
    }

    private async Task AuthenticateAsOrganizerAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestApiModel("organizer@seatsync.demo", "demo123"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        payload.Should().NotBeNull();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
    }
} 
