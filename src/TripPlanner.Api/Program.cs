using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using TripPlanner.Adapters.Persistence.Ef;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

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
var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=tripplanner.db";
services.AddEfPersistence(
    builder.Configuration.GetConnectionString("Default")!,
    builder.Environment);
// --- AUTH ---
services.AddJwtAuth(builder.Configuration);

// ---------------
// BUILD the app
// ---------------
var app = builder.Build();

// Auto-migrate DB
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex)
        when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name"))
    {
        // local DB drifted; rebuild
        var path = Path.Combine(AppContext.BaseDirectory, "tripplanner.db");
        if (File.Exists(path)) File.Delete(path);
        await db.Database.MigrateAsync();
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
v1.MapDestinationEndpoints();
v1.MapInvitesEndpoints();
v1.MapParticipantsEndpoints();
v1.MapTripsEndpoints();

// ---------------------------
// AUTH endpoints (public)
// ---------------------------
app.MapPost("/auth/register", async (RegisterRequest req, IUserRepository users, CancellationToken ct) =>
{
    var existing = await users.FindByEmail(req.Email, ct);
    if (existing is not null)
        return Results.Conflict(new { error = "Email already registered." });

    var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    var user = new UserRecord
    {
        Email = req.Email,
        DisplayName = req.DisplayName,
        PasswordHash = hash
    };
    await users.Add(user, ct);
    return Results.Created($"/users/{user.UserId}", new { userId = user.UserId, user.DisplayName, user.Email });
}).AllowAnonymous();

app.MapPost("/auth/login", async (LoginRequest req, IUserRepository users, IJwtService jwt, JwtOptions jwtOptions, IConfiguration cfg, CancellationToken ct) =>
{
    var user = await users.FindByEmail(req.Email, ct);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var (access, exp) = jwt.IssueAccessToken(user.UserId, user.Email);
    var rawRefresh = jwt.IssueRefreshToken();
    
    var refreshPepper = cfg["Jwt:RefreshPepper"];
    var refreshHash = TokenHasher.Hash(rawRefresh, refreshPepper);

    await users.AddRefreshToken(new RefreshTokenRecord
    {
        UserId = user.UserId,
        Token = refreshHash,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.RefreshTokenDays)
    }, ct);

    return Results.Ok(new LoginResponse(access, rawRefresh, (int)TimeSpan.FromMinutes(jwtOptions.AccessTokenMinutes).TotalSeconds));
}).AllowAnonymous();

app.MapPost("/auth/refresh", async (RefreshRequest req, IUserRepository users, IJwtService jwt, JwtOptions jwtOpts, IConfiguration cfg, CancellationToken ct) =>
{
    var pepper = cfg["Jwt:RefreshPepper"];
    var presentedHash = TokenHasher.Hash(req.RefreshToken, pepper);

    var token = await users.FindRefreshToken(presentedHash, ct);
    if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        return Results.Unauthorized();

    var user = token.User!;
    var (access, _) = jwt.IssueAccessToken(user.UserId, user.Email);

    var newRefreshRaw = jwt.IssueRefreshToken();
    var newRefreshHash = TokenHasher.Hash(newRefreshRaw, pepper);

    token.RevokedAt = DateTimeOffset.UtcNow;
    await users.AddRefreshToken(new RefreshTokenRecord
    {
        UserId = user.UserId,
        Token = newRefreshHash,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOpts.RefreshTokenDays)
    }, ct);
    await users.SaveChanges(ct);

    return Results.Ok(new RefreshResponse(
        access,
        newRefreshRaw,
        (int)TimeSpan.FromMinutes(jwtOpts.AccessTokenMinutes).TotalSeconds));
}).AllowAnonymous();

app.MapPost("/auth/logout", async (RefreshRequest req, IUserRepository users, CancellationToken ct) =>
{
    var token = await users.FindRefreshToken(req.RefreshToken, ct);
    if (token is null) return Results.NoContent();
    token.RevokedAt = DateTimeOffset.UtcNow;
    await users.SaveChanges(ct);
    return Results.NoContent();
}).AllowAnonymous();

// ---- PROTECT existing API ----
v1.RequireAuthorization(); // everything under /api/v1 now requires a valid JWT

app.Run();