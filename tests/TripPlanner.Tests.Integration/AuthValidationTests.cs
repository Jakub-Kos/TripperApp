using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace TripPlanner.Tests.Integration;

public class AuthValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ENVIRONMENT", "Development");
        });
    }

    private static async Task RegisterAsync(HttpClient c, string email, string password, string displayName)
    {
        var resp = await c.PostAsJsonAsync("/auth/register", new { email, password, displayName });
        if ((int)resp.StatusCode == 201) return;
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@t.local";

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/my/trips");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        var email = NewEmail("wrongpass");
        await RegisterAsync(client, email, "correct", "User");

        var resp = await client.PostAsJsonAsync("/auth/login", new { email, password = "incorrect" });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Malformed_token_returns_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        var resp = await client.GetAsync("/api/v1/my/trips");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private static string CreateJwt(string key, string issuer, string audience, Guid userId, string email, DateTimeOffset? notBefore = null, DateTimeOffset? expires = null)
    {
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var now = DateTimeOffset.UtcNow;
        var nbf = (notBefore ?? now).UtcDateTime;
        var exp = (expires ?? now.AddMinutes(10)).UtcDateTime;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var jwt = new JwtSecurityToken(issuer: issuer, audience: audience, claims: claims, notBefore: nbf, expires: exp, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [Fact]
    public async Task Token_signed_with_wrong_key_returns_401()
    {
        var client = _factory.CreateClient();
        var devIssuer = "https://localhost";
        var devAudience = "tripplanner";
        var wrongKey = "this-is-the-wrong-key-which-should-not-validate-123456";
        var token = CreateJwt(wrongKey, devIssuer, devAudience, Guid.NewGuid(), "x@y.local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/v1/my/trips");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_with_wrong_audience_returns_401()
    {
        var client = _factory.CreateClient();
        // Use the dev key from appsettings.Development.json (kept in repo for tests)
        var devKey = "dev-only-please-override-with-secret";
        var devIssuer = "https://localhost";
        var wrongAudience = "wrong-aud";
        var token = CreateJwt(devKey, devIssuer, wrongAudience, Guid.NewGuid(), "x@y.local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/v1/my/trips");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_token_returns_401()
    {
        var client = _factory.CreateClient();
        var devKey = "dev-only-please-override-with-secret";
        var devIssuer = "https://localhost";
        var devAudience = "tripplanner";
        // Expired 10 minutes ago; notBefore 20 minutes ago. ClockSkew is 2 minutes -> should be invalid.
        var token = CreateJwt(devKey, devIssuer, devAudience, Guid.NewGuid(), "x@y.local", DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow.AddMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/v1/my/trips");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
