using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Core.Application.Application.Trips;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Api.Endpoints;

public static class TripsEndpoints
{
    public record UpdateTripStatusRequest(bool IsFinished);
    
    public static IEndpointRouteBuilder MapTripsEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/trips",
                async (CreateTripRequest req, CreateTripHandler handler, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var organizer)) return Results.Unauthorized();

                    // If CreateTripCommand expects a string organizerId, pass organizer.ToString("D")
                    var result = await handler.Handle(new CreateTripCommand(req.Name, organizer.ToString("D")), ct);
                    return Results.Created($"/api/v1/trips/{result.Trip.TripId}", result.Trip);
                })
            .AddEndpointFilter(new ValidationFilter<CreateTripRequest>())
            .WithTags("Trips")
            .WithName("CreateTrip")
            .WithSummary("Create a new trip")
            .WithDescription("Creates a trip with the current user as organizer.")
            .Accepts<CreateTripRequest>("application/json")
            .Produces<TripDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
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

        v1.MapGet("/my/trips",
                async (bool? includeFinished, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var query = db.Trips
                        .AsNoTracking()
                        .Where(t => t.OrganizerId == me || t.Participants.Any(p => p.UserId == me));

                    if (includeFinished != true)
                        query = query.Where(t => !t.IsFinished);

                    var trips = await query
                        .OrderBy(t => t.Name)
                        .Select(t => new TripDto(
                            t.TripId.ToString(),
                            t.Name,
                            t.OrganizerId.ToString()
                        ))
                        .ToListAsync(ct);

                    return Results.Ok(trips);
                })
            .WithTags("Trips")
            .WithName("ListMyTrips")
            .WithSummary("List my trips")
            .WithDescription("Returns trips where the current user is a participant. 'includeFinished=false' by default.")
            .Produces<IReadOnlyList<TripDto>>(StatusCodes.Status200OK);

        v1.MapPatch("/trips/{tripId:guid}/status",
                async (Guid tripId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, UpdateTripStatusRequest req, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                    if (trip is null) return Results.NotFound();

                    if (trip.OrganizerId != me) return Results.Forbid();

                    trip.IsFinished = req.IsFinished;
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                })
            .WithTags("Trips")
            .WithSummary("Set trip finished flag")
            .WithDescription("Organizer can mark a trip finished or unfinished.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
        
        return v1;
    }
}