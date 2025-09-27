using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Core.Application.Application.Trips.Commands;
using TripPlanner.Core.Application.Application.Trips.Queries;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Api.Endpoints;

public static class DestinationEndpoints
{
    public record DestinationProxyVoteRequest(string ParticipantId);
    
    public static IEndpointRouteBuilder MapDestinationEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/trips/{tripId:guid}/destinations/{destinationId:guid}/votes",
                async (Guid tripId, Guid destinationId, System.Security.Claims.ClaimsPrincipal user, VoteDestinationHandler handler, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var ok = await handler.Handle(new VoteDestinationCommand(tripId.ToString("D"), destinationId.ToString("D"), me), ct);
                    if (!ok) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    return Results.NoContent();
                })
            .WithTags("Destinations")
            .WithSummary("Vote self for destination")
            .WithDescription("Current user votes using their participant identity.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/{tripId:guid}/destinations/{destinationId:guid}/votes/proxy",
                async (Guid tripId, Guid destinationId, DestinationProxyVoteRequest req, AppDbContext db, CancellationToken ct) =>
                {
                    if (!Guid.TryParse(req.ParticipantId, out var participantId))
                        return Results.BadRequest("Invalid ParticipantId.");

                    var placeholder = await db.Participants.FirstOrDefaultAsync(
                        p => p.TripId == tripId && p.ParticipantId == participantId && p.UserId == null, ct);

                    if (placeholder is null) return Results.BadRequest("Only placeholders can be proxied or participant not found.");

                    var destExists = await db.Destinations.AnyAsync(d => d.TripId == tripId && d.DestinationId == destinationId, ct);
                    if (!destExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    var dup = await db.DestinationVotes.AnyAsync(v => v.DestinationId == destinationId && v.ParticipantId == participantId, ct);
                    if (!dup)
                    {
                        db.DestinationVotes.Add(new DestinationVoteRecord { DestinationId = destinationId, ParticipantId = participantId });
                        await db.SaveChangesAsync(ct);
                    }

                    return Results.NoContent();
                })
            .WithTags("Destinations")
            .WithSummary("Proxy vote for destination")
            .WithDescription("Cast a vote on behalf of a placeholder participant.")
            .Accepts<DestinationProxyVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
        
        v1.MapGet("/trips/{tripId}/destinations",
                async (string tripId, ListDestinationsHandler handler, CancellationToken ct) =>
                {
                    var list = await handler.Handle(new ListDestinationsQuery(tripId), ct);
                    return list is null
                        ? Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"))
                        : Results.Ok(list);
                })
            .WithTags("Destinations")
            .WithName("ListDestinations")
            .WithSummary("List destination proposals")
            .WithDescription("Returns all destination proposals for the trip.")
            .Produces<IReadOnlyList<DestinationProposalDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

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
            .WithTags("Destinations")
            .WithName("ProposeDestination")
            .WithSummary("Propose destination")
            .WithDescription("Proposes a destination with title, optional description, and image URLs.")
            .Accepts<ProposeDestinationRequest>("application/json")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);
        
        return v1;
    }
}