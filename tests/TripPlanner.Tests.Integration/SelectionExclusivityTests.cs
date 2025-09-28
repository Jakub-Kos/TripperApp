using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class SelectionExclusivityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SelectionExclusivityTests(WebApplicationFactory<Program> factory)
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
    public async Task Destination_Choose_IsExclusive()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("dest-org");
        await RegisterAsync(client, email, "1", "Org D");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Dest choose" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Propose two destinations
            var d1 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "A", description = "" , imageUrls = Array.Empty<string>()});
            d1.EnsureSuccessStatusCode();
            var id1 = (await d1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;
            var d2 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "B", description = "" , imageUrls = Array.Empty<string>()});
            d2.EnsureSuccessStatusCode();
            var id2 = (await d2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;

            // Choose first then second
            (await client.PatchAsync($"/api/v1/trips/{tripId}/destinations/{id1}/choose", null)).EnsureSuccessStatusCode();
            (await client.PatchAsync($"/api/v1/trips/{tripId}/destinations/{id2}/choose", null)).EnsureSuccessStatusCode();

            // List and verify exclusivity
            var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/trips/{tripId}/destinations");
            var items = list.EnumerateArray().ToArray();
            items.Length.Should().Be(2);
            var chosen = items.Single(x => x.GetProperty("isChosen").GetBoolean());
            chosen.GetProperty("destinationId").GetGuid().ToString("D").Should().Be(id2);
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, access, tripId);
            await DeleteUserMeAsync(client, access);
        }
    }

    [Fact]
    public async Task Transportation_Choose_IsExclusive()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("trans-org");
        await RegisterAsync(client, email, "1", "Org T");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Trans choose" });
            createTrip.EnsureSuccessStatusCode();
            tripId = (await createTrip.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tripId").GetString()!;

            // Create two transportations
            var t1 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/transportations", new { title = "A", description = "" });
            t1.EnsureSuccessStatusCode();
            var id1 = (await t1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("transportationId").GetString()!;
            var t2 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/transportations", new { title = "B", description = "" });
            t2.EnsureSuccessStatusCode();
            var id2 = (await t2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("transportationId").GetString()!;

            // Choose first then second
            (await client.PatchAsync($"/api/v1/trips/{tripId}/transportations/{id1}/choose", null)).EnsureSuccessStatusCode();
            (await client.PatchAsync($"/api/v1/trips/{tripId}/transportations/{id2}/choose", null)).EnsureSuccessStatusCode();

            // List and verify exclusivity
            var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/trips/{tripId}/transportations");
            var items = list.EnumerateArray().ToArray();
            items.Length.Should().Be(2);
            var chosen = items.Single(x => x.GetProperty("isChosen").GetBoolean());
            chosen.GetProperty("transportationId").GetGuid().ToString("D").Should().Be(id2);
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, access, tripId);
            await DeleteUserMeAsync(client, access);
        }
    }

    [Fact]
    public async Task TermProposal_Choose_IsExclusive()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("term-org");
        await RegisterAsync(client, email, "1", "Org Term");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Term choose" });
            createTrip.EnsureSuccessStatusCode();
            tripId = (await createTrip.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tripId").GetString()!;

            var start1 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5)).ToString("yyyy-MM-dd");
            var end1 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(6)).ToString("yyyy-MM-dd");
            var start2 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)).ToString("yyyy-MM-dd");
            var end2 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(8)).ToString("yyyy-MM-dd");

            var t1 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals", new { start = start1, end = end1 });
            t1.EnsureSuccessStatusCode();
            var term1 = (await t1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("termProposalId").GetString()!;
            var t2 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals", new { start = start2, end = end2 });
            t2.EnsureSuccessStatusCode();
            var term2 = (await t2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("termProposalId").GetString()!;

            // Choose first then second (organizer)
            (await client.PatchAsync($"/api/v1/trips/{tripId}/term-proposals/{term1}", null)).EnsureSuccessStatusCode();
            (await client.PatchAsync($"/api/v1/trips/{tripId}/term-proposals/{term2}", null)).EnsureSuccessStatusCode();

            // Verify via term proposals list that only term2 is chosen
            var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/trips/{tripId}/term-proposals");
            var items = list.EnumerateArray().ToArray();
            items.Length.Should().Be(2);

            var chosen = items.Single(x => x.GetProperty("isChosen").GetBoolean());
            chosen.GetProperty("termProposalId").GetGuid().ToString("D").Should().Be(term2);

            // Also verify term1 is not chosen
            items.First(x => x.GetProperty("termProposalId").GetGuid().ToString("D") == term1).GetProperty("isChosen").GetBoolean().Should().BeFalse();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, access, tripId);
            await DeleteUserMeAsync(client, access);
        }
    }
}
