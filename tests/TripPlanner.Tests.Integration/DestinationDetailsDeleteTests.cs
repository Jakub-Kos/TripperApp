using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class DestinationDetailsDeleteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DestinationDetailsDeleteTests(WebApplicationFactory<Program> factory)
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
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
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
    public async Task Destination_GetDetail_And_Delete_By_Author_And_Forbidden_For_NonAuthor()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");

        // Login as organizer A
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Destination Detail/Delete Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip!.GetProperty("tripId").GetString()!;

            // Propose destination by A
            var propose = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "Lake", description = "Nice", imageUrls = Array.Empty<string>() });
            propose.EnsureSuccessStatusCode();
            var destId = (await propose.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;

            // Self vote by organizer A
            var voteA = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/votes", null);
            voteA.EnsureSuccessStatusCode();

            // GET detail and verify structure
            var getDetail = await client.GetAsync($"/api/v1/trips/{tripId}/destinations/{destId}");
            getDetail.EnsureSuccessStatusCode();
            var det = await getDetail.Content.ReadFromJsonAsync<JsonElement>();
            det.GetProperty("destinationId").GetGuid().ToString("D").Should().Be(destId);
            det.GetProperty("title").GetString().Should().Be("Lake");
            det.GetProperty("imageUrls").EnumerateArray().Count().Should().Be(0);
            det.GetProperty("createdByUserId").GetGuid().Should().NotBe(Guid.Empty);
            det.GetProperty("voters").EnumerateArray().Count().Should().Be(1);
            det.GetProperty("votesCount").GetInt32().Should().Be(1);

            // Invite and join B
            var invite = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 60, maxUses = 5 });
            invite.EnsureSuccessStatusCode();
            var code = (await invite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
            var (accessB, _) = await LoginAsync(client, emailB, "2");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
            (await client.PostAsJsonAsync("/api/v1/trips/join", new { code })).EnsureSuccessStatusCode();

            // Non-author/non-organizer tries to delete -> 403
            var delForbidden = await client.DeleteAsync($"/api/v1/trips/{tripId}/destinations/{destId}");
            delForbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Switch back to organizer (and author) A and delete -> 204
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
            var delOk = await client.DeleteAsync($"/api/v1/trips/{tripId}/destinations/{destId}");
            delOk.EnsureSuccessStatusCode();

            // Now GET detail should be 404
            var getMissing = await client.GetAsync($"/api/v1/trips/{tripId}/destinations/{destId}");
            getMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, (await LoginAsync(client, emailA, "1")).access);
            await DeleteUserMeAsync(client, (await LoginAsync(client, emailB, "2")).access);
        }
    }
}
