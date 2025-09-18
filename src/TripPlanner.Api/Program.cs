using TripPlanner.Adapters.Persistence.Ef;
// using TripPlanner.Adapters.Persistence.InMemory;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Application.Application.Trips;
using FluentValidation;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Core.Validation.Validators;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddValidatorsFromAssemblyContaining<CreateTripRequestValidator>();

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
app.MapPost("/api/v1/trips", 
    async (CreateTripRequest req, CreateTripHandler handler, CancellationToken ct) =>
    {
        var result = await handler.Handle(new CreateTripCommand(req.Name, req.OrganizerId), ct);
        return Results.Created($"/api/v1/trips/{result.Trip.TripId}", result.Trip);
    })
    .AddEndpointFilter(new ValidationFilter<CreateTripRequest>());

app.MapGet("/api/v1/trips", 
    async (int? skip, int? take, ListTripsHandler handler, CancellationToken ct) =>
    {
        var trips = await handler.Handle(new ListTripsQuery(skip ?? 0, take ?? 50), ct);
        return Results.Ok(trips);
    });

// NEW: GetTripById
app.MapGet("/api/v1/trips/{tripId}", 
    async (string tripId, GetTripByIdHandler handler, CancellationToken ct) =>
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
        // keep route param validation if you want, e.g. Guid.TryParse(tripId,...)
        var ok = await handler.Handle(new AddParticipantCommand(tripId, req.UserId), ct);
        return ok ? Results.NoContent()
                  : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
    })
    .AddEndpointFilter(new ValidationFilter<AddParticipantRequest>());

// NEW: ProposeDateOption
app.MapPost("/api/v1/trips/{tripId}/date-options",
    async (string tripId, ProposeDateRequest req, ProposeDateOptionHandler handler, CancellationToken ct) =>
    {
        var date = DateOnly.Parse(req.Date); // safe now thanks to validator
        var id = await handler.Handle(new ProposeDateOptionCommand(tripId, date), ct);
        return id is null
            ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
            : Results.Created($"/api/v1/trips/{tripId}", new { dateOptionId = id.Value.Value.ToString("D") });
    })
    .AddEndpointFilter(new ValidationFilter<ProposeDateRequest>());

// NEW: CastVote
app.MapPost("/api/v1/trips/{tripId}/votes",
    async (string tripId, CastVoteRequest req, CastVoteHandler handler, CancellationToken ct) =>
    {
        var ok = await handler.Handle(new CastVoteCommand(tripId, req.DateOptionId, req.UserId), ct);
        return ok ? Results.NoContent()
                  : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or option not found"));
    })
    .AddEndpointFilter(new ValidationFilter<CastVoteRequest>());

app.Run();
