using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class ParticipantsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ParticipantsApiTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task Users_Update_DisplayName_Works()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("user");
        await RegisterAsync(client, email, "1", "User X");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var patch = await client.PatchAsJsonAsync("/api/v1/users/me", new { displayName = "User Y" });
        patch.EnsureSuccessStatusCode();

        // cleanup
        await client.DeleteAsync("/api/v1/users/me");
    }

    [Fact]
    public async Task Participants_List_Patch_Delete()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Participants Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Create placeholder
            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest One" });
            createPh.EnsureSuccessStatusCode();
            var placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // List participants
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list.EnsureSuccessStatusCode();
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            arr.GetArrayLength().Should().BeGreaterThan(0);
            arr.EnumerateArray().Any(e => e.GetProperty("displayName").GetString() == "Guest One").Should().BeTrue();

            // Patch placeholder display name as organizer
            var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}/participants/{placeholderId}", new { displayName = "Guest Renamed" });
            patch.EnsureSuccessStatusCode();

            var list2 = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list2.EnsureSuccessStatusCode();
            var arr2 = await list2.Content.ReadFromJsonAsync<JsonElement>();
            arr2.EnumerateArray().Any(e => e.GetProperty("displayName").GetString() == "Guest Renamed").Should().BeTrue();

            // Non-organizer cannot delete
            var (accessB, _) = await LoginAsync(client, emailB, "2");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
            var delForbidden = await client.DeleteAsync($"/api/v1/trips/{tripId}/participants/{placeholderId}");
            delForbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Organizer can delete
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
            var delOk = await client.DeleteAsync($"/api/v1/trips/{tripId}/participants/{placeholderId}");
            delOk.EnsureSuccessStatusCode();

            // List again should not contain the placeholder
            var list3 = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list3.EnsureSuccessStatusCode();
            var arr3 = await list3.Content.ReadFromJsonAsync<JsonElement>();
            arr3.EnumerateArray().Any(e => e.GetProperty("participantId").GetString() == placeholderId).Should().BeFalse();
        }
        finally
        {
            if (tripId is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }

            // cleanup users
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailA, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailB, "2")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }

    [Fact]
    public async Task Participant_Can_Update_Own_Trip_DisplayName()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("selfrename");
        await RegisterAsync(client, email, "1", "Original User");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            // Create a trip (organizer becomes a real participant)
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Rename Me Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Update my participant display name within the trip
            var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}/participants/me", new { displayName = "Trip Nick" });
            patch.EnsureSuccessStatusCode();

            // Verify via participants list
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list.EnsureSuccessStatusCode();
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            arr.EnumerateArray().Any(e => e.GetProperty("isMe").GetBoolean() && e.GetProperty("displayName").GetString() == "Trip Nick").Should().BeTrue();
        }
        finally
        {
            if (tripId is not null)
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            // cleanup user
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, email, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }
}
