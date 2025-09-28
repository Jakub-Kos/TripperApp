using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class DateAndTermsGetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DateAndTermsGetTests(WebApplicationFactory<Program> factory)
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
    public async Task Date_Votes_Get_RawAvailability()
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
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Date Votes Get Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Create a placeholder participant
            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest AV" });
            createPh.EnsureSuccessStatusCode();
            var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Set range and vote: self + proxy same day
            var startIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd");
            var dateIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd");
            var endIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)).ToString("yyyy-MM-dd");
            (await client.PutAsJsonAsync($"/api/v1/trips/{tripId}/date-range", new { start = startIso, end = endIso })).EnsureSuccessStatusCode();

            // Self vote
            (await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes", new { date = dateIso })).EnsureSuccessStatusCode();
            // Proxy vote
            (await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes/proxy", new { date = dateIso, participantId = placeholderId })).EnsureSuccessStatusCode();

            // GET raw availability
            var get = await client.GetAsync($"/api/v1/trips/{tripId}/date-votes");
            get.EnsureSuccessStatusCode();
            var arr = await get.Content.ReadFromJsonAsync<JsonElement>();
            arr.ValueKind.Should().Be(JsonValueKind.Array);

            var item = arr.EnumerateArray().FirstOrDefault(e => e.GetProperty("date").GetString() == dateIso);
            item.ValueKind.Should().Be(JsonValueKind.Object);
            var votes = item.GetProperty("participantIds").EnumerateArray().Select(e => e.GetString()!).ToList();
            votes.Count.Should().Be(2);
            votes.All(id => Guid.TryParse(id, out _)).Should().BeTrue();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }

    [Fact]
    public async Task Term_List_and_GetVotes()
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
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Term GET Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Propose term
            var startIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd");
            var endIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)).ToString("yyyy-MM-dd");
            var prop = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals", new { start = startIso, end = endIso });
            prop.EnsureSuccessStatusCode();
            var termId = (await prop.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("termProposalId").GetString()!;

            // Create placeholder and cast proxy vote
            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest TV" });
            createPh.EnsureSuccessStatusCode();
            var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Self vote
            (await client.PostAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}/votes", null)).EnsureSuccessStatusCode();
            // Proxy vote
            (await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}/votes/proxy", new { participantId = placeholderId })).EnsureSuccessStatusCode();

            // GET list of proposals
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/term-proposals");
            list.EnsureSuccessStatusCode();
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            arr.ValueKind.Should().Be(JsonValueKind.Array);
            var item = arr.EnumerateArray().FirstOrDefault(t => t.GetProperty("termProposalId").GetGuid().ToString("D") == termId);
            item.ValueKind.Should().Be(JsonValueKind.Object);
            item.GetProperty("start").GetString().Should().Be(startIso);
            item.GetProperty("end").GetString().Should().Be(endIso);
            item.GetProperty("votesCount").GetInt32().Should().BeGreaterThan(0);

            // GET votes for the term
            var votesGet = await client.GetAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}/votes");
            votesGet.EnsureSuccessStatusCode();
            var votesArr = await votesGet.Content.ReadFromJsonAsync<JsonElement>();
            votesArr.ValueKind.Should().Be(JsonValueKind.Array);
            votesArr.GetArrayLength().Should().Be(2); // self + placeholder
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }
}
