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
    public async Task Health_and_Auth_work()
    {
        var client = _factory.CreateClient();
        // Health
        var health = await client.GetAsync("/health");
        health.EnsureSuccessStatusCode();
        // Register + Login
        await RegisterAsync(client, "1", "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, "1", "1");
        accessA.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Trip_Create_List_Get()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "1", "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, "1", "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        var tripName = $"Trip {DateTime.UtcNow:O}";
        var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = tripName });
        createTrip.EnsureSuccessStatusCode();
        var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = createdTrip.GetProperty("tripId").GetString()!;

        var listTrips = await client.GetAsync("/api/v1/my/trips");
        listTrips.EnsureSuccessStatusCode();
        var tripsJson = await listTrips.Content.ReadFromJsonAsync<JsonElement>();
        tripsJson.EnumerateArray().Any(e => e.GetProperty("name").GetString() == tripName).Should().BeTrue();

        var getTrip = await client.GetAsync($"/api/v1/trips/{tripId}");
        getTrip.EnsureSuccessStatusCode();
        var tripJson = await getTrip.Content.ReadFromJsonAsync<JsonElement>();
        tripJson.GetProperty("name").GetString().Should().Be(tripName);
    }

    [Fact]
    public async Task Invite_Create_and_Join()
    {
        var client = _factory.CreateClient();
        // Register A and B
        await RegisterAsync(client, "1", "1", "Alice A");
        await RegisterAsync(client, "2", "2", "Bob B");
        var (accessA, _) = await LoginAsync(client, "1", "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        // Create a trip
        var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Invite Trip" });
        createTrip.EnsureSuccessStatusCode();
        var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = createdTrip.GetProperty("tripId").GetString()!;
        // Create invite code
        var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
        inviteResp.EnsureSuccessStatusCode();
        var inviteJson = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
        var inviteCode = inviteJson.GetProperty("code").GetString()!;
        // Login B and join
        var (accessB, _) = await LoginAsync(client, "2", "2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
        joinResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Date_Propose_and_SelfVotes()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "1", "1", "Alice A");
        await RegisterAsync(client, "2", "2", "Bob B");
        // A creates trip
        var (accessA, _) = await LoginAsync(client, "1", "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Date Trip" });
        createTrip.EnsureSuccessStatusCode();
        var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = createdTrip.GetProperty("tripId").GetString()!;
        // Invite and join B
        var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
        inviteResp.EnsureSuccessStatusCode();
        var inviteCode = (await inviteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        var (accessB, _) = await LoginAsync(client, "2", "2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
        joinResp.EnsureSuccessStatusCode();
        // Propose a date
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var dateIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd");
        var proposeDate = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-options", new { date = dateIso });
        proposeDate.EnsureSuccessStatusCode();
        var dateOptionId = (await proposeDate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dateOptionId").GetString()!;
        // Vote A
        var voteDateA = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes", new { dateOptionId });
        voteDateA.EnsureSuccessStatusCode();
        // Vote B
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var voteDateB = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes", new { dateOptionId });
        voteDateB.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Destination_Propose_List_and_SelfVotes()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "1", "1", "Alice A");
        await RegisterAsync(client, "2", "2", "Bob B");
        // A creates trip
        var (accessA, _) = await LoginAsync(client, "1", "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Destination Trip" });
        createTrip.EnsureSuccessStatusCode();
        var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = createdTrip.GetProperty("tripId").GetString()!;
        // Invite and join B
        var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
        inviteResp.EnsureSuccessStatusCode();
        var inviteCode = (await inviteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        var (accessB, _) = await LoginAsync(client, "2", "2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
        joinResp.EnsureSuccessStatusCode();
        // Propose destination
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var proposeDest = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "Beach", description = "Sunny", imageUrls = Array.Empty<string>() });
        proposeDest.EnsureSuccessStatusCode();
        var destId = (await proposeDest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;
        // List
        var listDest = await client.GetAsync($"/api/v1/trips/{tripId}/destinations");
        listDest.EnsureSuccessStatusCode();
        // Vote A
        var voteDestA = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes", content: null);
        voteDestA.EnsureSuccessStatusCode();
        // Vote B
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var voteDestB = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes", content: null);
        voteDestB.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Placeholder_ProxyVote_and_Claim()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "1", "1", "Alice A");
        await RegisterAsync(client, "2", "2", "Bob B");
        // A creates trip
        var (accessA, _) = await LoginAsync(client, "1", "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Placeholder Trip" });
        createTrip.EnsureSuccessStatusCode();
        var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = createdTrip.GetProperty("tripId").GetString()!;
        // Propose destination (so we can vote on it)
        var proposeDest = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "Beach", description = "Sunny", imageUrls = Array.Empty<string>() });
        proposeDest.EnsureSuccessStatusCode();
        var destId = (await proposeDest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;
        // Create placeholder and claim code
        var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest X" });
        createPh.EnsureSuccessStatusCode();
        var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;
        var issueClaim = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders/{placeholderId}/claim-codes", new { expiresInMinutes = 30 });
        issueClaim.EnsureSuccessStatusCode();
        var claimCode = (await issueClaim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        // Proxy vote for placeholder
        var proxyVote = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes/proxy", new { participantId = placeholderId });
        proxyVote.EnsureSuccessStatusCode();
        // B claims placeholder
        var (accessB, _) = await LoginAsync(client, "2", "2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        var claim = await client.PostAsJsonAsync("/api/v1/trips/placeholders/claim", new { code = claimCode, displayName = "Bob B" });
        claim.EnsureSuccessStatusCode();
    }
}
