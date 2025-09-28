using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using TripPlanner.Adapters.Persistence.Ef;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Api.Infrastructure.Time;

using TripPlanner.Api.Auth;
using TripPlanner.Api.Endpoints;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Api.Swagger;
using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Application.Application.Trips;
using TripPlanner.Core.Application.Application.Trips.Commands;
using TripPlanner.Core.Application.Application.Trips.Queries;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Primitives;
using TripPlanner.Core.Validation.Validators;

// ---------------------------
// BUILD + SERVICES (register)
// ---------------------------
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Validation
services.AddValidatorsFromAssemblyContaining<CreateTripRequestValidator>();

// Swagger
services.AddEndpointsApiExplorer();
services.AddTripPlannerSwagger();

// JSON (force camelCase)
services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});

// Handlers
services.AddScoped<CreateTripHandler>();
services.AddScoped<ListTripsHandler>();
services.AddScoped<GetTripByIdHandler>();
services.AddScoped<AddParticipantHandler>();
services.AddScoped<ProposeDateOptionHandler>();
services.AddScoped<CastVoteHandler>();
services.AddScoped<ListDestinationsHandler>();
services.AddScoped<ProposeDestinationHandler>();
services.AddScoped<VoteDestinationHandler>();

// Persistence (EF + SQLite)
bool IsRunningInTests()
{
    try
    {
        if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_TEST_HOST"), "true", StringComparison.OrdinalIgnoreCase))
            return true;
        var friendly = AppDomain.CurrentDomain.FriendlyName;
        if (!string.IsNullOrEmpty(friendly) && friendly.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
    }
    catch { }
    return false;
}
var isRunningInTests = IsRunningInTests();
var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=tripplanner.db";
if (isRunningInTests)
{
    // In tests, use an isolated DB per test host to avoid interfering with the developer's local DB
    cs = $"Data Source=tripplanner-tests-{Guid.NewGuid():N}.db";
}
services.AddEfPersistence(
    cs,
    builder.Environment);
// --- AUTH ---
services.AddJwtAuth(builder.Configuration);
services.AddSingleton<IClock, SystemClock>();

// ---------------
// BUILD the app
// ---------------
var app = builder.Build();

// Auto-create DB schema when missing (no destructive reset)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
    }
    catch
    {
        // Ignore; DB likely already created. We avoid destructive resets to keep dev/test stability.
    }
}

// ---------------------------
// MIDDLEWARE (post-Build())
// ---------------------------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TripPlanner API v1");
    c.RoutePrefix = "swagger";
});

// Serve static files from wwwroot (for uploaded images)
app.UseStaticFiles();

// Auth pipeline – order matters
app.UseAuthentication();
app.UseAuthorization();

// Public endpoints
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

// ---------------------------
// API v1 (protected group)
// ---------------------------
// Trips
var v1 = app.MapGroup("/api/v1");
v1.MapDateEndpoints();
v1.MapTermEndpoints();
v1.MapDestinationEndpoints();
v1.MapTransportationEndpoints();
v1.MapInvitesEndpoints();
v1.MapParticipantsEndpoints();
v1.MapTripsEndpoints();
v1.MapUsersEndpoints();
v1.MapItineraryEndpoints();
v1.MapGearEndpoints();

// ---------------------------
// AUTH endpoints (public)
// ---------------------------
app.MapPost("/auth/register", async (RegisterRequest req, IUserRepository users, CancellationToken ct) =>
{
    var existing = await users.FindByEmail(req.Email, ct);
    if (existing is not null)
        return Results.Conflict(new { error = "Email already registered." });

    var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    var userId = await users.Add(req.Email, req.DisplayName, hash, ct);
    return Results.Created($"/users/{userId}", new { userId, displayName = req.DisplayName, email = req.Email });
}).AllowAnonymous().WithTags("Auth");

app.MapPost("/auth/login", async (LoginRequest req, IUserRepository users, IJwtService jwt, JwtOptions jwtOptions, IConfiguration cfg, IClock clock, CancellationToken ct) =>
{
    var user = await users.FindByEmail(req.Email, ct);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var (access, _) = jwt.IssueAccessToken(user.UserId, user.Email);
    var rawRefresh = jwt.IssueRefreshToken();
    
    var refreshPepper = cfg["Jwt:RefreshPepper"];
    var refreshHash = TokenHasher.Hash(rawRefresh, refreshPepper);

    await users.AddRefreshToken(user.UserId, refreshHash, clock.UtcNow.AddDays(jwtOptions.RefreshTokenDays), ct);

    return Results.Json(new LoginResponse(access, rawRefresh, (int)TimeSpan.FromMinutes(jwtOptions.AccessTokenMinutes).TotalSeconds));
}).AllowAnonymous().WithTags("Auth");

app.MapPost("/auth/refresh", async (RefreshRequest req, IUserRepository users, IJwtService jwt, JwtOptions jwtOpts, IConfiguration cfg, IClock clock, CancellationToken ct) =>
{
    var pepper = cfg["Jwt:RefreshPepper"];
    var presentedHash = TokenHasher.Hash(req.RefreshToken, pepper);

    var token = await users.FindRefreshToken(presentedHash, ct);
    if (token is null || token.RevokedAt is not null || token.ExpiresAt <= clock.UtcNow)
        return Results.Unauthorized();

    var user = token.User!;
    var (access, _) = jwt.IssueAccessToken(user.UserId, user.Email);

    var newRefreshRaw = jwt.IssueRefreshToken();
    var newRefreshHash = TokenHasher.Hash(newRefreshRaw, pepper);

    await users.RevokeRefreshToken(presentedHash, clock.UtcNow, ct);
    await users.AddRefreshToken(user.UserId, newRefreshHash, clock.UtcNow.AddDays(jwtOpts.RefreshTokenDays), ct);

    return Results.Json(new RefreshResponse(
        access,
        newRefreshRaw,
        (int)TimeSpan.FromMinutes(jwtOpts.AccessTokenMinutes).TotalSeconds));
}).AllowAnonymous().WithTags("Auth");

app.MapPost("/auth/logout", async (RefreshRequest req, IUserRepository users, IConfiguration cfg, IClock clock, CancellationToken ct) =>
{
    var pepper = cfg["Jwt:RefreshPepper"];
    var presentedHash = TokenHasher.Hash(req.RefreshToken, pepper);
    await users.RevokeRefreshToken(presentedHash, clock.UtcNow, ct);
    return Results.NoContent();
}).AllowAnonymous().WithTags("Auth");

// ---- PROTECT existing API ----
v1.RequireAuthorization(); // everything under /api/v1 now requires a valid JWT

app.Run();

public partial class Program { }