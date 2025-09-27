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

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@t.local";

    private static async Task DeleteTripAsync(HttpClient c, string accessToken, string tripId)
    {
        var prev = c.DefaultRequestHeaders.Authorization;
        try
        {
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            await c.DeleteAsync($"/api/v1/trips/{tripId}");
        }
        finally
        {
            c.DefaultRequestHeaders.Authorization = prev;
        }
    }

    private static async Task DeleteUserMeAsync(HttpClient c, string accessToken)
    {
        var prev = c.DefaultRequestHeaders.Authorization;
        try
        {
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            await c.DeleteAsync("/api/v1/users/me");
        }
        finally
        {
            c.DefaultRequestHeaders.Authorization = prev;
        }
    }

    [Fact]
    public async Task Health_and_Auth_work()
    {
        var client = _factory.CreateClient();
        // Health
        var health = await client.GetAsync("/health");
        health.EnsureSuccessStatusCode();
        // Register + Login
        var email = NewEmail("alice");
        await RegisterAsync(client, email, "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, email, "1");
        accessA.Should().NotBeNullOrEmpty();
        // cleanup
        await DeleteUserMeAsync(client, accessA);
    }

    [Fact]
    public async Task Trip_Create_List_Get()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        await RegisterAsync(client, emailA, "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            var tripName = $"Trip {DateTime.UtcNow:O}";
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = tripName });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            var listTrips = await client.GetAsync("/api/v1/my/trips");
            listTrips.EnsureSuccessStatusCode();
            var tripsJson = await listTrips.Content.ReadFromJsonAsync<JsonElement>();
            tripsJson.EnumerateArray().Any(e => e.GetProperty("name").GetString() == tripName).Should().BeTrue();

            var getTrip = await client.GetAsync($"/api/v1/trips/{tripId}");
            getTrip.EnsureSuccessStatusCode();
            var tripJson = await getTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripJson.GetProperty("name").GetString().Should().Be(tripName);
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }

    [Fact]
    public async Task Invite_Create_and_Join()
    {
        var client = _factory.CreateClient();
        // Register A and B
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        string? tripId = null;
        string? accessB = null;
        try
        {
            // Create a trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Invite Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;
            // Create invite code
            var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
            inviteResp.EnsureSuccessStatusCode();
            var inviteJson = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
            var inviteCode = inviteJson.GetProperty("code").GetString()!;
            // Login B and join
            (accessB, _) = await LoginAsync(client, emailB, "2");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
            var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
            joinResp.EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
            if (accessB is not null) await DeleteUserMeAsync(client, accessB);
        }
    }

    [Fact]
    public async Task Date_Propose_and_SelfVotes()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");
        // A creates trip
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        string? tripId = null;
        string? accessB = null;
        try
        {
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Date Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;
            // Invite and join B
            var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
            inviteResp.EnsureSuccessStatusCode();
            var inviteCode = (await inviteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
            (accessB, _) = await LoginAsync(client, emailB, "2");
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
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
            if (accessB is not null) await DeleteUserMeAsync(client, accessB);
        }
    }

    [Fact]
    public async Task Destination_Propose_List_and_SelfVotes()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");
        // A creates trip
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        string? tripId = null;
        string? accessB = null;
        try
        {
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Destination Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;
            // Invite and join B
            var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
            inviteResp.EnsureSuccessStatusCode();
            var inviteCode = (await inviteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
            (accessB, _) = await LoginAsync(client, emailB, "2");
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
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
            if (accessB is not null) await DeleteUserMeAsync(client, accessB);
        }
    }

    [Fact]
    public async Task Placeholder_ProxyVote_and_Claim()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");
        // A creates trip
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        string? tripId = null;
        string? accessB = null;
        try
        {
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Placeholder Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;
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
            (accessB, _) = await LoginAsync(client, emailB, "2");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
            var claim = await client.PostAsJsonAsync("/api/v1/trips/placeholders/claim", new { code = claimCode, displayName = "Bob B" });
            claim.EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
            if (accessB is not null) await DeleteUserMeAsync(client, accessB);
        }
    }

    [Fact]
    public async Task Trip_Status_and_List_Filter()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        await RegisterAsync(client, emailA, "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Finished Trip" });
            createTrip.EnsureSuccessStatusCode();
            var created = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = created.GetProperty("tripId").GetString()!;

            // Initially should be listed (unfinished)
            var list1 = await client.GetAsync("/api/v1/my/trips");
            list1.EnsureSuccessStatusCode();
            var arr1 = await list1.Content.ReadFromJsonAsync<JsonElement>();
            arr1.EnumerateArray().Any(e => e.GetProperty("tripId").GetString() == tripId).Should().BeTrue();

            // Mark finished
            var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}/status", new { isFinished = true });
            patch.EnsureSuccessStatusCode();

            // Now default list (includeFinished=false) should not include
            var list2 = await client.GetAsync("/api/v1/my/trips");
            list2.EnsureSuccessStatusCode();
            var arr2 = await list2.Content.ReadFromJsonAsync<JsonElement>();
            arr2.EnumerateArray().Any(e => e.GetProperty("tripId").GetString() == tripId).Should().BeFalse();

            // But includeFinished=true should include
            var list3 = await client.GetAsync("/api/v1/my/trips?includeFinished=true");
            list3.EnsureSuccessStatusCode();
            var arr3 = await list3.Content.ReadFromJsonAsync<JsonElement>();
            arr3.EnumerateArray().Any(e => e.GetProperty("tripId").GetString() == tripId).Should().BeTrue();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }

    [Fact]
    public async Task Auth_Refresh_and_Logout()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("auth");
        await RegisterAsync(client, email, "1", "Auth User");
        var (access, refresh) = await LoginAsync(client, email, "1");

        // Refresh
        var refreshResp = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = refresh });
        refreshResp.EnsureSuccessStatusCode();
        var refreshJson = await refreshResp.Content.ReadFromJsonAsync<JsonElement>();
        var newAccess = refreshJson.GetProperty("accessToken").GetString()!;
        var newRefresh = refreshJson.GetProperty("refreshToken").GetString()!;

        // Logout (revoke refresh)
        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = newRefresh });
        logout.EnsureSuccessStatusCode();

        // Refresh again should fail
        var refreshAgain = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = newRefresh });
        refreshAgain.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        // Cleanup user using access (either original or new)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAccess);
        await DeleteUserMeAsync(client, newAccess);
    }

    [Fact]
    public async Task Date_ProxyVote_for_Placeholder()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        await RegisterAsync(client, emailA, "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Date Proxy Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Propose date
            var dateIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)).ToString("yyyy-MM-dd");
            var proposeDate = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-options", new { date = dateIso });
            proposeDate.EnsureSuccessStatusCode();
            var dateOptionId = (await proposeDate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dateOptionId").GetString()!;

            // Create placeholder
            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest P" });
            createPh.EnsureSuccessStatusCode();
            var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Proxy vote for date option
            var proxyVote = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes/proxy", new { dateOptionId, participantId = placeholderId });
            proxyVote.EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }
}
