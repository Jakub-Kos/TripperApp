using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class SmokeApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SmokeApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ENVIRONMENT", "Development");
        });
    }

    private static async Task RegisterAsync(HttpClient c, string email, string password, string displayName)
    {
        var resp = await c.PostAsJsonAsync("/auth/register", new { email, password, displayName });
        if ((int)resp.StatusCode == 201)
            return; // created
        // if already exists, ok for idempotent flows
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    private static async Task<(string access, string refresh)> LoginAsync(HttpClient c, string email, string password)
    {
        var resp = await c.PostAsJsonAsync("/auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (obj.GetProperty("accessToken").GetString()!, obj.GetProperty("refreshToken").GetString()!);
    }

    [Fact]
    public async Task Full_smoke_flow_works()
    {
        var client = _factory.CreateClient();

        // Health
        var health = await client.GetAsync("/health");
        health.EnsureSuccessStatusCode();

        // Register A and B
        await RegisterAsync(client, "1", "1", "Alice A");
        await RegisterAsync(client, "2", "2", "Bob B");

        // Login A
        var (accessA, _) = await LoginAsync(client, "1", "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        // Create trip
        var tripName = $"Smoke Trip {DateTime.UtcNow:O}";
        var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = tripName });
        createTrip.EnsureSuccessStatusCode();
        var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = createdTrip.GetProperty("tripId").GetString()!;

        // List my trips (after creating one, it should appear)
        var listTrips = await client.GetAsync("/api/v1/my/trips");
        listTrips.EnsureSuccessStatusCode();
        var tripsJson = await listTrips.Content.ReadFromJsonAsync<JsonElement>();
        tripsJson.ValueKind.Should().Be(JsonValueKind.Array);
        tripsJson.EnumerateArray().Any(e => e.GetProperty("name").GetString() == tripName).Should().BeTrue();

        // Get trip by id
        var getTrip = await client.GetAsync($"/api/v1/trips/{tripId}");
        getTrip.EnsureSuccessStatusCode();
        var tripJson = await getTrip.Content.ReadFromJsonAsync<JsonElement>();
        tripJson.GetProperty("name").GetString().Should().Be(tripName);

        // Create invite code
        var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
        inviteResp.EnsureSuccessStatusCode();
        var inviteJson = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
        var inviteCode = inviteJson.GetProperty("code").GetString()!;

        // Login B
        var (accessB, _) = await LoginAsync(client, "2", "2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);

        // Join trip by code (B)
        var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
        joinResp.EnsureSuccessStatusCode();

        // Propose date
        var dateIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd");
        var proposeDate = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-options", new { date = dateIso });
        proposeDate.EnsureSuccessStatusCode();
        var dateJson = await proposeDate.Content.ReadFromJsonAsync<JsonElement>();
        var dateOptionId = dateJson.GetProperty("dateOptionId").GetString()!;

        // Vote date (A)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var voteDateA = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes", new { dateOptionId });
        voteDateA.EnsureSuccessStatusCode();

        // Vote date (B)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var voteDateB = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes", new { dateOptionId });
        voteDateB.EnsureSuccessStatusCode();

        // Propose destination
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var proposeDest = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "Beach", description = "Sunny", imageUrls = Array.Empty<string>() });
        proposeDest.EnsureSuccessStatusCode();
        var destJson = await proposeDest.Content.ReadFromJsonAsync<JsonElement>();
        var destId = destJson.GetProperty("destinationId").GetString()!;

        // List destinations
        var listDest = await client.GetAsync($"/api/v1/trips/{tripId}/destinations");
        listDest.EnsureSuccessStatusCode();

        // Vote destination (A)
        var voteDestA = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes", content: null);
        voteDestA.EnsureSuccessStatusCode();

        // Vote destination (B)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var voteDestB = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes", content: null);
        voteDestB.EnsureSuccessStatusCode();

        // Placeholder: create and issue claim code
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest X" });
        createPh.EnsureSuccessStatusCode();
        var phJson = await createPh.Content.ReadFromJsonAsync<JsonElement>();
        var placeholderId = phJson.GetProperty("participantId").GetString()!;

        var issueClaim = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders/{placeholderId}/claim-codes", new { expiresInMinutes = 30 });
        issueClaim.EnsureSuccessStatusCode();
        var claimJson = await issueClaim.Content.ReadFromJsonAsync<JsonElement>();
        var claimCode = claimJson.GetProperty("code").GetString()!;

        // Proxy vote for placeholder
        var proxyVote = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes/proxy", new { participantId = placeholderId });
        proxyVote.EnsureSuccessStatusCode();

        // Claim placeholder as B
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var claim = await client.PostAsJsonAsync("/api/v1/trips/placeholders/claim", new { code = claimCode, displayName = "Bob B" });
        claim.EnsureSuccessStatusCode();
    }
}
