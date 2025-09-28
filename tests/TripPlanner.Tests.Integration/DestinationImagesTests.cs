using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class DestinationImagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DestinationImagesTests(WebApplicationFactory<Program> factory)
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
    public async Task Destination_Upload_Png_Jpeg_And_List()
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
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Img Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Propose destination
            var proposeDest = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "Mountain", description = "Snow", imageUrls = Array.Empty<string>() });
            proposeDest.EnsureSuccessStatusCode();
            var destId = (await proposeDest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;

            // Prepare multipart with png and jpeg
            using var mp = new MultipartFormDataContent();
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 }; // fake PNG header
            var jpgBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7, 8 }; // fake JPEG header
            var png = new ByteArrayContent(pngBytes);
            png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            mp.Add(png, "files", "a.png");
            var jpg = new ByteArrayContent(jpgBytes);
            jpg.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            mp.Add(jpg, "files", "b.jpg");

            var upload = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/images", mp);
            upload.EnsureSuccessStatusCode();
            var upJson = await upload.Content.ReadFromJsonAsync<JsonElement>();
            var urls = upJson.GetProperty("urls").EnumerateArray().Select(e => e.GetString()!).ToList();
            urls.Count.Should().Be(2);
            urls.All(u => u.Contains("/uploads/destinations/")).Should().BeTrue();

            // List destinations and verify urls present
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/destinations");
            list.EnsureSuccessStatusCode();
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            var found = arr.EnumerateArray().First(e => e.GetProperty("destinationId").GetGuid().ToString("D") == destId);
            var listedUrls = found.GetProperty("imageUrls").EnumerateArray().Select(e => e.GetString()!).ToList();
            urls.All(u => listedUrls.Contains(u)).Should().BeTrue();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }

    [Fact]
    public async Task Destination_Upload_Enforces_Max_10()
    {
        var client = _factory.CreateClient();
        var emailA = NewEmail("alice");
        await RegisterAsync(client, emailA, "1", "Alice A");
        var (accessA, _) = await LoginAsync(client, emailA, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessA);

        string? tripId = null;
        try
        {
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Img Limit Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            var proposeDest = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", new { title = "City", description = "Fun", imageUrls = Array.Empty<string>() });
            proposeDest.EnsureSuccessStatusCode();
            var destId = (await proposeDest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("destinationId").GetString()!;

            // First upload 9 small images
            using (var mp9 = new MultipartFormDataContent())
            {
                for (int i = 0; i < 9; i++)
                {
                    var bytes = new byte[] { 0xFF, 0xD8, 0xFF, (byte)i };
                    var part = new ByteArrayContent(bytes);
                    part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    mp9.Add(part, "files", $"{i}.jpg");
                }
                var up = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/images", mp9);
                up.EnsureSuccessStatusCode();
            }

            // Now try to upload 2 more -> should be 400
            using var mp2 = new MultipartFormDataContent();
            for (int i = 0; i < 2; i++)
            {
                var bytes = new byte[] { 0x89, 0x50, 0x4E, (byte)i };
                var part = new ByteArrayContent(bytes);
                part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                mp2.Add(part, "files", $"x{i}.png");
            }
            var up2 = await client.PostAsync($"/api/v1/trips/{tripId}/destinations/{destId}/images", mp2);
            up2.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, accessA, tripId);
            await DeleteUserMeAsync(client, accessA);
        }
    }
}
