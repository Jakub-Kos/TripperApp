using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class PlaceholderProxyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlaceholderProxyTests(WebApplicationFactory<Program> factory)
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
    public async Task Date_ProxyDeleteVote_for_Placeholder()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        await RegisterAsync(client, emailA, "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Date Proxy Delete Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip!.GetProperty("tripId").GetString()!;

            var startIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd");
            var dateIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd");
            var endIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)).ToString("yyyy-MM-dd");
            (await client.PutAsJsonAsync($"/api/v1/trips/{tripId}/date-range", new { start = startIso, end = endIso })).EnsureSuccessStatusCode();

            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest D" });
            createPh.EnsureSuccessStatusCode();
            var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Proxy vote
            (await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes/proxy", new { date = dateIso, participantId = placeholderId })).EnsureSuccessStatusCode();

            // Proxy delete vote (idempotent)
            var del1 = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/date-votes/proxy")
            {
                Content = JsonContent.Create(new { date = dateIso, participantId = placeholderId })
            };
            (await client.SendAsync(del1)).EnsureSuccessStatusCode();
            var del2 = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/date-votes/proxy")
            {
                Content = JsonContent.Create(new { date = dateIso, participantId = placeholderId })
            };
            (await client.SendAsync(del2)).EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }

    [Fact]
    public async Task Term_ProxyVote_and_Delete_for_Placeholder()
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
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Term Proxy Vote/Delete Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip!.GetProperty("tripId").GetString()!;

            // Propose term
            var startIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd");
            var endIso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)).ToString("yyyy-MM-dd");
            var prop = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals", new { start = startIso, end = endIso });
            prop.EnsureSuccessStatusCode();
            var termId = (await prop.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("termProposalId").GetString()!;

            // Create placeholder
            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest T" });
            createPh.EnsureSuccessStatusCode();
            var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Proxy vote
            (await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}/votes/proxy", new { participantId = placeholderId })).EnsureSuccessStatusCode();

            // Proxy delete (idempotent)
            var del1 = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/term-proposals/{termId}/votes/proxy")
            {
                Content = JsonContent.Create(new { participantId = placeholderId })
            };
            (await client.SendAsync(del1)).EnsureSuccessStatusCode();
            var del2 = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/term-proposals/{termId}/votes/proxy")
            {
                Content = JsonContent.Create(new { participantId = placeholderId })
            };
            (await client.SendAsync(del2)).EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }
}