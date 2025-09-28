using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class ClaimAndJoinFlowsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ClaimAndJoinFlowsTests(WebApplicationFactory<Program> factory)
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
    public async Task Claiming_Placeholder_Converts_It_To_Real_User_And_Marks_IsMe()
    {
        var client = _factory.CreateClient();
        var emailOrg = NewEmail("org");
        var emailUser = NewEmail("user");
        await RegisterAsync(client, emailOrg, "1", "Organizer");
        await RegisterAsync(client, emailUser, "1", "UserOne");
        var (orgAccess, _) = await LoginAsync(client, emailOrg, "1");
        var (userAccess, _) = await LoginAsync(client, emailUser, "1");

        string? tripId = null;
        string? placeholderId = null;
        try
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
            // Create trip
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Claim Flow Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            // Create placeholder
            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest One" });
            createPh.EnsureSuccessStatusCode();
            placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Issue claim code (organizer only)
            var issue = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders/{placeholderId}/claim-codes", new { expiresInMinutes = 30 });
            issue.EnsureSuccessStatusCode();
            var claimObj = await issue.Content.ReadFromJsonAsync<JsonElement>();
            var code = claimObj.GetProperty("code").GetString()!;

            // Claim as user
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAccess);
            var claimResp = await client.PostAsJsonAsync("/api/v1/trips/placeholders/claim", new { code, displayName = "User Claimed" });
            claimResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Verify via participants list
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list.EnsureSuccessStatusCode();
            var participants = await list.Content.ReadFromJsonAsync<JsonElement>();
            var me = participants.EnumerateArray().FirstOrDefault(e => e.GetProperty("participantId").GetString() == placeholderId);
            me.GetProperty("isPlaceholder").GetBoolean().Should().BeFalse();
            me.GetProperty("isMe").GetBoolean().Should().BeTrue();
            me.GetProperty("displayName").GetString().Should().Be("User Claimed");
        }
        finally
        {
            if (tripId is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailOrg, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailUser, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }

    [Fact]
    public async Task Join_By_Invite_Then_Claiming_Another_Placeholder_Fails_And_Keeps_Single_Participant()
    {
        var client = _factory.CreateClient();
        var emailOrg = NewEmail("org");
        var emailUser = NewEmail("user");
        await RegisterAsync(client, emailOrg, "1", "Organizer");
        await RegisterAsync(client, emailUser, "1", "UserTwo");
        var (orgAccess, _) = await LoginAsync(client, emailOrg, "1");
        var (userAccess, _) = await LoginAsync(client, emailUser, "1");

        string? tripId = null;
        string? placeholderId = null;
        try
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
            // Create trip & placeholder
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Join then Claim Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest A" });
            createPh.EnsureSuccessStatusCode();
            placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Create invite code
            var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 30, maxUses = 5 });
            inviteResp.EnsureSuccessStatusCode();
            var inviteObj = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
            var inviteCode = inviteObj.GetProperty("code").GetString()!;

            // User joins by invite
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAccess);
            var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
            joinResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Organizer issues claim code for existing placeholder
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
            var issue = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders/{placeholderId}/claim-codes", new { expiresInMinutes = 30 });
            issue.EnsureSuccessStatusCode();
            var claimObj = await issue.Content.ReadFromJsonAsync<JsonElement>();
            var claimCode = claimObj.GetProperty("code").GetString()!;

            // User attempts to claim -> should fail because they already have a participant
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAccess);
            var claimResp = await client.PostAsJsonAsync("/api/v1/trips/placeholders/claim", new { code = claimCode, displayName = "Should Not Work" });
            claimResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Verify only one participant belongs to the user (IsMe true exactly once)
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list.EnsureSuccessStatusCode();
            var participants = await list.Content.ReadFromJsonAsync<JsonElement>();
            participants.EnumerateArray().Count(e => e.GetProperty("isMe").GetBoolean()).Should().Be(1);
        }
        finally
        {
            if (tripId is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailOrg, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailUser, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }

    [Fact]
    public async Task Claim_First_Then_Join_By_Invite_Does_Not_Create_Duplicate_Participant()
    {
        var client = _factory.CreateClient();
        var emailOrg = NewEmail("org");
        var emailUser = NewEmail("user");
        await RegisterAsync(client, emailOrg, "1", "Organizer");
        await RegisterAsync(client, emailUser, "1", "UserThree");
        var (orgAccess, _) = await LoginAsync(client, emailOrg, "1");
        var (userAccess, _) = await LoginAsync(client, emailUser, "1");

        string? tripId = null;
        string? placeholderId = null;
        try
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
            // Create trip & placeholder
            var createTrip = await client.PostAsJsonAsync("/api/v1/trips", new { name = "Claim then Join Trip" });
            createTrip.EnsureSuccessStatusCode();
            var createdTrip = await createTrip.Content.ReadFromJsonAsync<JsonElement>();
            tripId = createdTrip.GetProperty("tripId").GetString()!;

            var createPh = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName = "Guest X" });
            createPh.EnsureSuccessStatusCode();
            placeholderId = (await createPh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participantId").GetString()!;

            // Issue claim code and claim as user
            var issue = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders/{placeholderId}/claim-codes", new { expiresInMinutes = 30 });
            issue.EnsureSuccessStatusCode();
            var claimObj = await issue.Content.ReadFromJsonAsync<JsonElement>();
            var claimCode = claimObj.GetProperty("code").GetString()!;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAccess);
            var claimResp = await client.PostAsJsonAsync("/api/v1/trips/placeholders/claim", new { code = claimCode });
            claimResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Organizer creates invite; user joins (should be idempotent)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
            var inviteResp = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes = 30, maxUses = 5 });
            inviteResp.EnsureSuccessStatusCode();
            var inviteCode = (await inviteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAccess);
            var joinResp = await client.PostAsJsonAsync("/api/v1/trips/join", new { code = inviteCode });
            joinResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Verify still only one participant for user
            var list = await client.GetAsync($"/api/v1/trips/{tripId}/participants");
            list.EnsureSuccessStatusCode();
            var participants = await list.Content.ReadFromJsonAsync<JsonElement>();
            participants.EnumerateArray().Count(e => e.GetProperty("isMe").GetBoolean()).Should().Be(1);
        }
        finally
        {
            if (tripId is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccess);
                await client.DeleteAsync($"/api/v1/trips/{tripId}");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailOrg, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(client, emailUser, "1")).access);
            await client.DeleteAsync("/api/v1/users/me");
        }
    }
}
