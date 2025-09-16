using TripPlanner.Adapters.Persistence.Ef;
// using TripPlanner.Adapters.Persistence.InMemory;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Application.Application.Trips;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

// Handlers (existing + new)
services.AddScoped<CreateTripHandler>();
services.AddScoped<ListTripsHandler>();
services.AddScoped<GetTripByIdHandler>();
services.AddScoped<AddParticipantHandler>();
services.AddScoped<ProposeDateOptionHandler>();
services.AddScoped<CastVoteHandler>();

// Persistence (EF)
var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=tripplanner.db";
services.AddEfPersistence(cs);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Existing
app.MapPost("/api/v1/trips", async (CreateTripRequest req, CreateTripHandler handler, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest(new ErrorResponse(ErrorCodes.Validation, "Name is required."));
    if (!Guid.TryParse(req.OrganizerId, out _))
        return Results.BadRequest(new ErrorResponse(ErrorCodes.Validation, "OrganizerId must be a GUID."));

    var result = await handler.Handle(new CreateTripCommand(req.Name, req.OrganizerId), ct);
    return Results.Created($"/api/v1/trips/{result.Trip.TripId}", result.Trip);
});

app.MapGet("/api/v1/trips", async (int? skip, int? take, ListTripsHandler handler, CancellationToken ct) =>
{
    var trips = await handler.Handle(new ListTripsQuery(skip ?? 0, take ?? 50), ct);
    return Results.Ok(trips);
});

// NEW: GetTripById
app.MapGet("/api/v1/trips/{tripId}", async (string tripId, GetTripByIdHandler handler, CancellationToken ct) =>
{
    var dto = await handler.Handle(new GetTripByIdQuery(tripId), ct);
    return dto is null
        ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
        : Results.Ok(dto);
});

// NEW: AddParticipant
app.MapPost("/api/v1/trips/{tripId}/participants",
    async (string tripId, AddParticipantRequest req, AddParticipantHandler handler, CancellationToken ct) =>
{
    if (!Guid.TryParse(req.UserId, out _))
        return Results.BadRequest(new ErrorResponse(ErrorCodes.Validation, "UserId must be a GUID."));

    var ok = await handler.Handle(new AddParticipantCommand(tripId, req.UserId), ct);
    return ok ? Results.NoContent()
              : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
});

// NEW: ProposeDateOption
app.MapPost("/api/v1/trips/{tripId}/date-options",
    async (string tripId, ProposeDateRequest req, ProposeDateOptionHandler handler, CancellationToken ct) =>
{
    if (!DateOnly.TryParse(req.Date, out var date))
        return Results.BadRequest(new ErrorResponse(ErrorCodes.Validation, "Date must be YYYY-MM-DD."));

    var id = await handler.Handle(new ProposeDateOptionCommand(tripId, date), ct);
    return id is null
        ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
        : Results.Created($"/api/v1/trips/{tripId}", new { dateOptionId = id.Value.Value.ToString("D") });
});

// NEW: CastVote
app.MapPost("/api/v1/trips/{tripId}/votes",
    async (string tripId, CastVoteRequest req, CastVoteHandler handler, CancellationToken ct) =>
{
    var ok = await handler.Handle(new CastVoteCommand(tripId, req.DateOptionId, req.UserId), ct);
    return ok ? Results.NoContent()
              : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or option not found"));
});

app.Run();
