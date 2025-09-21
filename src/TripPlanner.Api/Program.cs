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
using TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

using TripPlanner.Api.Auth;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Api.Swagger;
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
v1.MapPost("/trips",
        async (CreateTripRequest req, CreateTripHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new CreateTripCommand(req.Name, req.OrganizerId), ct);
            return Results.Created($"/api/v1/trips/{result.Trip.TripId}", result.Trip);
        })
    .AddEndpointFilter(new ValidationFilter<CreateTripRequest>())
    .WithTags("Trips")
    .WithName("CreateTrip")
    .WithSummary("Create a new trip")
    .WithDescription("Creates a trip with the given name and organizer GUID.")
    .Accepts<CreateTripRequest>("application/json")
    .Produces<TripDto>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

v1.MapGet("/trips",
        async (int? skip, int? take, ListTripsHandler handler, CancellationToken ct) =>
        {
            var trips = await handler.Handle(new ListTripsQuery(skip ?? 0, take ?? 50), ct);
            return Results.Ok(trips);
        })
    .WithTags("Trips")
    .WithName("ListTrips")
    .WithSummary("List trips")
    .WithDescription("Returns a paged list of trips (default: 0..50).")
    .Produces<IReadOnlyList<TripDto>>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

v1.MapGet("/trips/{tripId}",
        async (string tripId, GetTripByIdHandler handler, CancellationToken ct) =>
        {
            var dto = await handler.Handle(new GetTripByIdQuery(tripId), ct);
            return dto is null
                ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
                : Results.Ok(dto);
        })
    .WithTags("Trips")
    .WithName("GetTripById")
    .WithSummary("Get trip details")
    .WithDescription("Returns participants and date options for the given trip.")
    .Produces<TripSummaryDto>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

v1.MapPost("/trips/{tripId}/participants",
        async (Guid tripId, AddParticipantRequest req, AddParticipantHandler handler, CancellationToken ct) =>
        {
            if (req.UserId is not Guid userId)            
                return Results.BadRequest("UserId must be a GUID.");
            var ok = await handler.Handle(new AddParticipantCommand(tripId.ToString(), userId.ToString()), ct);
            return ok ? Results.NoContent() : Results.NotFound();
        })
    .AddEndpointFilter(new ValidationFilter<AddParticipantRequest>())
    .WithTags("Trips")
    .WithName("AddParticipant")
    .WithSummary("Add participant")
    .WithDescription("Adds a participant (GUID) to the trip.")
    .Accepts<AddParticipantRequest>("application/json")
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

v1.MapPost("/trips/{tripId}/date-options",
        async (string tripId, ProposeDateRequest req, ProposeDateOptionHandler handler, CancellationToken ct) =>
        {
            var date = DateOnly.Parse(req.Date); // safe now thanks to validator
            var id = await handler.Handle(new ProposeDateOptionCommand(tripId, date), ct);
            return id is null
                ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
                : Results.Created($"/api/v1/trips/{tripId}", new { dateOptionId = id.Value.Value.ToString("D") });
        })
    .AddEndpointFilter(new ValidationFilter<ProposeDateRequest>())
    .WithTags("Trips")
    .WithName("ProposeDateOption")
    .WithSummary("Propose date option")
    .WithDescription("Proposes a date (YYYY-MM-DD) for the trip.")
    .Accepts<ProposeDateRequest>("application/json")
    .Produces(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

v1.MapPost("/trips/{tripId}/votes",
        async (string tripId, CastVoteRequest req, CastVoteHandler handler, CancellationToken ct) =>
        {
            var ok = await handler.Handle(new CastVoteCommand(tripId, req.DateOptionId, req.UserId), ct);
            return ok
                ? Results.NoContent()
                : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or option not found"));
        })
    .AddEndpointFilter(new ValidationFilter<CastVoteRequest>())
    .WithTags("Trips")
    .WithName("CastVote")
    .WithSummary("Cast a vote")
    .WithDescription("Casts a vote for a specific date option (GUID) as the given user (GUID).")
    .Accepts<CastVoteRequest>("application/json")
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// --- Destinations: list ---
v1.MapGet("/trips/{tripId}/destinations",
        async (string tripId, ListDestinationsHandler handler, CancellationToken ct) =>
        {
            var list = await handler.Handle(new ListDestinationsQuery(tripId), ct);
            return list is null
                ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
                : Results.Ok(list);
        })
    .WithTags("Trips")
    .WithName("ListDestinations")
    .WithSummary("List destination proposals")
    .WithDescription("Returns all destination proposals for the trip.")
    .Produces<IReadOnlyList<DestinationProposalDto>>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

// PROPOSE
v1.MapPost("/trips/{tripId}/destinations",
        async (string tripId, ProposeDestinationRequest req, ProposeDestinationHandler handler, CancellationToken ct) =>
        {
            var id = await handler.Handle(new ProposeDestinationCommand(tripId, req.Title, req.Description, req.ImageUrls), ct);
            return id is null
                ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
                : Results.Created($"/api/v1/trips/{tripId}/destinations/{id.Value.Value:D}",
                    new { destinationId = id.Value.Value.ToString("D") });
        })
    .AddEndpointFilter(new ValidationFilter<ProposeDestinationRequest>())
    .WithTags("Trips")
    .WithName("ProposeDestination")
    .WithSummary("Propose destination")
    .WithDescription("Proposes a destination with title, optional description, and image URLs.")
    .Accepts<ProposeDestinationRequest>("application/json")
    .Produces(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// VOTE
v1.MapPost("/trips/{tripId}/destinations/{destinationId}/votes",
        async (string tripId, string destinationId, VoteDestinationRequest req, VoteDestinationHandler handler, CancellationToken ct) =>
        {
            var ok = await handler.Handle(new VoteDestinationCommand(tripId, destinationId, req.UserId), ct);
            return ok
                ? Results.NoContent()
                : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));
        })
    .AddEndpointFilter(new ValidationFilter<VoteDestinationRequest>())
    .WithTags("Trips")
    .WithName("VoteDestination")
    .WithSummary("Cast a vote for a destination")
    .WithDescription("Casts a vote for a specific destination (GUID in route) as the given user (GUID).")
    .Accepts<VoteDestinationRequest>("application/json")
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

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
