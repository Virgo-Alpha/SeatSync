using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SeatSync.Api.Contracts.Events;
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
}