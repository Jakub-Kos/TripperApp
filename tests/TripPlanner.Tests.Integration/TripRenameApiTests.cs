using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class TripRenameApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TripRenameApiTests(WebApplicationFactory<Program> factory)
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
    public async Task Organizer_can_rename_trip_and_Get_returns_updated_name()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("alice");
        await RegisterAsync(client, email, "1", "Alice A");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            var create = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Original Name" });
            create.EnsureSuccessStatusCode();
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            tripId = created.GetProperty("tripId").GetString()!;

            var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { name = "Renamed Trip" });
            patch.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var get = await client.GetAsync($"/api/v1/trips/{tripId}");
            get.EnsureSuccessStatusCode();
            var trip = await get.Content.ReadFromJsonAsync<JsonElement>();
            trip.GetProperty("name").GetString().Should().Be("Renamed Trip");
        }
        finally
        {
            if (tripId is not null)
            {
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }
            // cleanup user
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, email, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }

    [Fact]
    public async Task Rename_requires_non_empty_name_and_returns_400_with_error_code()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("user");
        await RegisterAsync(client, email, "1", "U S");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            var create = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Original" });
            create.EnsureSuccessStatusCode();
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            tripId = created.GetProperty("tripId").GetString()!;

            var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { name = "  \t  " });
            patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var err = await patch.Content.ReadFromJsonAsync<JsonElement>();
            err.TryGetProperty("code", out var codeProp).Should().BeTrue();
            codeProp.GetString().Should().Be("validation_error");
        }
        finally
        {
            if (tripId is not null)
            {
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, email, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }

    [Fact]
    public async Task Rename_nonexistent_trip_returns_404()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("user");
        await RegisterAsync(client, email, "1", "U S");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{Guid.NewGuid():D}", new { name = "X" });
        patch.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // cleanup user
        await client.DeleteAsync("/api/v1/users/me");
    }

    [Fact]
    public async Task Non_organizer_cannot_rename_trip_returns_403()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        var emailB = NewEmail("bob");
        await RegisterAsync(client, emailA, "1", "Alice A");
        await RegisterAsync(client, emailB, "2", "Bob B");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        var (accessB, _) = await LoginAsync(client, emailB, "2");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        string? tripId = null;
        try
        {
            var create = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Original" });
            create.EnsureSuccessStatusCode();
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            tripId = created.GetProperty("tripId").GetString()!;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
            var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { name = "Hacked" });
            patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            if (tripId is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailA, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailB, "2")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }

    [Fact]
    public async Task Unauthorized_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var patch = await client.PatchAsJsonAsync($"/api/v1/trips/{Guid.NewGuid():D}", new { name = "X" });
        patch.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
