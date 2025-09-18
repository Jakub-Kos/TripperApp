using TripPlanner.Adapters.Persistence.Ef;
// using TripPlanner.Adapters.Persistence.InMemory;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Application.Application.Trips;
using FluentValidation;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Core.Validation.Validators;

using Microsoft.AspNetCore.Http;
using TripPlanner.Api.Swagger;
using TripPlanner.Api.Swagger.Examples;
using Swashbuckle.AspNetCore.Filters; // not strictly required, but fine
using Microsoft.OpenApi.Models;       // if you later customize WithOpenApi

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddValidatorsFromAssemblyContaining<CreateTripRequestValidator>();

services.AddEndpointsApiExplorer();
services.AddTripPlannerSwagger();
// services.AddEndpointsApiExplorer();
// services.AddSwaggerGen();

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
    .AddEndpointFilter(new ValidationFilter<CreateTripRequest>())
    .WithTags("Trips")
    .WithName("CreateTrip")
    .WithSummary("Create a new trip")
    .WithDescription("Creates a trip with the given name and organizer GUID.")
    .Accepts<CreateTripRequest>("application/json")
    .Produces<TripDto>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

app.MapGet("/api/v1/trips", 
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

// NEW: GetTripById
app.MapGet("/api/v1/trips/{tripId}", 
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

// NEW: AddParticipant
app.MapPost("/api/v1/trips/{tripId}/participants",
    async (string tripId, AddParticipantRequest req, AddParticipantHandler handler, CancellationToken ct) =>
    {
        // keep route param validation if you want, e.g. Guid.TryParse(tripId,...)
        var ok = await handler.Handle(new AddParticipantCommand(tripId, req.UserId), ct);
        return ok ? Results.NoContent()
                  : Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
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
    .AddEndpointFilter(new ValidationFilter<ProposeDateRequest>())
    .WithTags("Trips")
    .WithName("ProposeDateOption")
    .WithSummary("Propose date option")
    .WithDescription("Proposes a date (YYYY-MM-DD) for the trip.")
    .Accepts<ProposeDateRequest>("application/json")
    .Produces(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// NEW: CastVote
app.MapPost("/api/v1/trips/{tripId}/votes",
    async (string tripId, CastVoteRequest req, CastVoteHandler handler, CancellationToken ct) =>
    {
        var ok = await handler.Handle(new CastVoteCommand(tripId, req.DateOptionId, req.UserId), ct);
        return ok ? Results.NoContent()
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

app.Run();
