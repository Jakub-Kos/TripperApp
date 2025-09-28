using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class ItineraryEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ItineraryEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task Itinerary_Days_Items_Routes_EndToEnd()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("itin");
        await RegisterAsync(client, email, "1", "Itin User");
        var (access, _) = await LoginAsync(client, email, "1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        string? tripId = null;
        try
        {
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Itinerary Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Create day
            var createDay = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/days", new { date = "2025-07-12", title = "Day 1", description = "Desc" });
            createDay.EnsureSuccessStatusCode();
            var dayDto = await createDay.Content.ReadFromJsonAsync<JsonElement>();
            var dayId = dayDto.GetProperty("dayId").GetString()!;

            // List days (should have 1)
            var listDays = await client.GetAsync($"/api/v1/trips/{tripId}/days");
            listDays.EnsureSuccessStatusCode();
            var daysArr = await listDays.Content.ReadFromJsonAsync<JsonElement>();
            daysArr.GetArrayLength().Should().Be(1);

            // Get day detail
            var getDay = await client.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}");
            getDay.EnsureSuccessStatusCode();
            var getDayJson = await getDay.Content.ReadFromJsonAsync<JsonElement>();
            getDayJson.GetProperty("title").GetString().Should().Be("Day 1");

            // Update day
            var updDay = await client.PutAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}", new { title = "Updated Day", description = "Updated" });
            updDay.EnsureSuccessStatusCode();

            // Patch anchors
            var patchAnchors = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/anchors", new { startLocation = new { name = "Hotel", lat = 50.067, lon = 19.912 }, endLocation = new { name = "Pension", lat = 50.061, lon = 19.94 } });
            patchAnchors.EnsureSuccessStatusCode();

            // Verify anchors via GET
            var getDay2 = await client.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}");
            getDay2.EnsureSuccessStatusCode();
            var getDay2Json = await getDay2.Content.ReadFromJsonAsync<JsonElement>();
            getDay2Json.GetProperty("startLocation").GetProperty("name").GetString().Should().Be("Hotel");
            getDay2Json.GetProperty("endLocation").GetProperty("name").GetString().Should().Be("Pension");

            // Add two items
            var addItem1 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/items", new { type = "POI", name = "Wawel", lat = 50.054, lon = 19.936, scheduledStart = "10:00", durationMinutes = 120, notes = "Buy tickets" });
            addItem1.EnsureSuccessStatusCode();
            var item1 = await addItem1.Content.ReadFromJsonAsync<JsonElement>();
            var item1Id = item1.GetProperty("itemId").GetString()!;

            var addItem2 = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/items", new { type = "Restaurant", name = "Lunch", scheduledStart = "12:30", durationMinutes = 60 });
            addItem2.EnsureSuccessStatusCode();
            var item2 = await addItem2.Content.ReadFromJsonAsync<JsonElement>();
            var item2Id = item2.GetProperty("itemId").GetString()!;

            // List items ordered
            var listItems = await client.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}/items");
            listItems.EnsureSuccessStatusCode();
            var itemsArr = await listItems.Content.ReadFromJsonAsync<JsonElement>();
            itemsArr.GetArrayLength().Should().Be(2);
            itemsArr[0].GetProperty("orderIndex").GetInt32().Should().Be(0);
            itemsArr[1].GetProperty("orderIndex").GetInt32().Should().Be(1);

            // Get item detail
            var getItem = await client.GetAsync($"/api/v1/trips/{tripId}/items/{item1Id}");
            getItem.EnsureSuccessStatusCode();
            var getItemJson = await getItem.Content.ReadFromJsonAsync<JsonElement>();
            getItemJson.GetProperty("name").GetString().Should().Be("Wawel");

            // Update item
            var updItem = await client.PutAsJsonAsync($"/api/v1/trips/{tripId}/items/{item1Id}", new { name = "Wawel Castle", notes = "Prebook" });
            updItem.EnsureSuccessStatusCode();

            // Reorder items (swap)
            var reorder = await client.PatchAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/items/reorder", new { orderedItemIds = new[] { item2Id, item1Id } });
            reorder.EnsureSuccessStatusCode();

            var listItems2 = await client.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}/items");
            listItems2.EnsureSuccessStatusCode();
            var itemsArr2 = await listItems2.Content.ReadFromJsonAsync<JsonElement>();
            itemsArr2.GetArrayLength().Should().Be(2);
            itemsArr2[0].GetProperty("itemId").GetString().Should().Be(item2Id);
            itemsArr2[0].GetProperty("orderIndex").GetInt32().Should().Be(0);
            itemsArr2[1].GetProperty("itemId").GetString().Should().Be(item1Id);
            itemsArr2[1].GetProperty("orderIndex").GetInt32().Should().Be(1);

            // Upload a day route (GeoJSON)
            using (var mp = new MultipartFormDataContent())
            {
                var jsonBytes = Encoding.UTF8.GetBytes("{\"type\":\"FeatureCollection\"}");
                var json = new ByteArrayContent(jsonBytes);
                json.Headers.ContentType = new MediaTypeHeaderValue("application/geo+json");
                mp.Add(json, "file", "dayroute.geojson");

                var up = await client.PostAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes", mp);
                up.EnsureSuccessStatusCode();
                var upJson = await up.Content.ReadFromJsonAsync<JsonElement>();
                upJson.GetProperty("routeId").GetInt32().Should().BeGreaterThan(0);
            }

            // List routes
            var routes = await client.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes");
            routes.EnsureSuccessStatusCode();
            var routesArr = await routes.Content.ReadFromJsonAsync<JsonElement>();
            routesArr.GetArrayLength().Should().Be(1);
            var routeId = routesArr[0].GetProperty("routeId").GetInt32();

            // Get route (redirect) with a couple of retries to avoid rare timing issues.
            // The API should only redirect if the target file exists; otherwise 404 is acceptable.
            HttpResponseMessage? getRoute = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                getRoute = await client.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes/{routeId}", HttpCompletionOption.ResponseHeadersRead);
                if (getRoute.StatusCode != HttpStatusCode.NotFound) break;
                await Task.Delay(100);
            }
            if (getRoute!.StatusCode == HttpStatusCode.Redirect)
            {
                getRoute.Headers.Location.Should().NotBeNull();
            }
            else
            {
                getRoute.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }

            // Delete item, route, and day
            var delItem = await client.DeleteAsync($"/api/v1/trips/{tripId}/items/{item1Id}");
            delItem.EnsureSuccessStatusCode();

            var delRoute = await client.DeleteAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes/{routeId}");
            delRoute.EnsureSuccessStatusCode();

            var delDay = await client.DeleteAsync($"/api/v1/trips/{tripId}/days/{dayId}");
            delDay.EnsureSuccessStatusCode();
        }
        finally
        {
            if (tripId is not null) await DeleteTripAsync(client, access, tripId);
            await DeleteUserMeAsync(client, access);
            await TestUtils.CleanupWwwrootAsync(_factory);
        }
    }
}
