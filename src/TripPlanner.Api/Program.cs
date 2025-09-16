using TripPlanner.Adapters.Persistence.InMemory;
using TripPlanner.Core.Application.Application.Trips;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Contracts.Common;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

// Handlers
services.AddScoped<CreateTripHandler>();
services.AddScoped<ListTripsHandler>();

// In-memory persistence (swap later for EF)
services.AddInMemoryPersistence();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

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

app.Run();