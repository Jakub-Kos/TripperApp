using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class TransportationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TransportationEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task Transportation_CRUD_And_FileUploads_Work()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("user");
        await RegisterAsync(client, email, "1", "User U");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Transport Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Create transportation
            var createT = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/transportations", new { title = "Train to Start", description = "Morning train" });
            createT.EnsureSuccessStatusCode();
            var createdT = await createT.Content.ReadFromJsonAsync<JsonElement>();
            var transportationId = createdT.GetProperty("transportationId").GetString()!;

            // List should contain the item
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/transportations");
            list.EnsureSuccessStatusCode();
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            arr.EnumerateArray().Any(e => e.GetProperty("transportationId").GetGuid().ToString("D") == transportationId).Should().BeTrue();

            // Update
            var upd = await client.PutAsJsonAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}", new { title = "Bus to Start", description = "Evening bus" });
            upd.EnsureSuccessStatusCode();

            // Upload a GPX and JSON as route files
            using (var mp = new MultipartFormDataContent())
            {
                var gpxBytes = new byte[] { 0x3C, 0x67, 0x70, 0x78, 1, 2 }; // '<gpx'...
                var gpx = new ByteArrayContent(gpxBytes);
                gpx.Headers.ContentType = new MediaTypeHeaderValue("application/gpx+xml");
                mp.Add(gpx, "files", "route.gpx");

                var jsonBytes = System.Text.Encoding.UTF8.GetBytes("{\"type\":\"FeatureCollection\"}");
                var json = new ByteArrayContent(jsonBytes);
                json.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                mp.Add(json, "files", "route.json");

                var upRoutes = await client.PostAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/routes", mp);
                upRoutes.EnsureSuccessStatusCode();
                var routesJson = await upRoutes.Content.ReadFromJsonAsync<JsonElement>();
                routesJson.GetProperty("routes").GetArrayLength().Should().Be(2);
            }

            // List routes
            var routesList = await client.GetAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/routes");
            routesList.EnsureSuccessStatusCode();
            var routesArr = await routesList.Content.ReadFromJsonAsync<JsonElement>();
            routesArr.GetArrayLength().Should().Be(2);
            var firstRouteId = routesArr.EnumerateArray().First().GetProperty("id").GetInt32();

            // Upload documents: one image and one pdf
            using (var mpd = new MultipartFormDataContent())
            {
                var jpgBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 5, 6 };
                var jpg = new ByteArrayContent(jpgBytes);
                jpg.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                mpd.Add(jpg, "files", "ticket.jpg");

                var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 1, 2 }; // %PDF
                var pdf = new ByteArrayContent(pdfBytes);
                pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                mpd.Add(pdf, "files", "accommodation.pdf");

                var upDocs = await client.PostAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/documents", mpd);
                upDocs.EnsureSuccessStatusCode();
                var docsJson = await upDocs.Content.ReadFromJsonAsync<JsonElement>();
                docsJson.GetProperty("documents").GetArrayLength().Should().Be(2);
            }

            // List docs
            var docsList = await client.GetAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/documents");
            docsList.EnsureSuccessStatusCode();
            var docsArr = await docsList.Content.ReadFromJsonAsync<JsonElement>();
            docsArr.GetArrayLength().Should().Be(2);
            var firstDocId = docsArr.EnumerateArray().First().GetProperty("id").GetInt32();

            // Delete one route and one document
            var delRoute = await client.DeleteAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/routes/{firstRouteId}");
            delRoute.EnsureSuccessStatusCode();
            var delDoc = await client.DeleteAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/documents/{firstDocId}");
            delDoc.EnsureSuccessStatusCode();

            // Delete transportation
            var delT = await client.DeleteAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}");
            delT.EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, access, tripId);
            await DeleteUserMeAsync(client, access);
            await TestUtils.CleanupWwwrootAsync(_factory);
        }
    }
}
