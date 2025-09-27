using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Core.Application.Application.Trips;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Api.Endpoints;

public static class DateEndpoints
{
    public record DateSelfVoteRequest(string DateOptionId);
    public record DateProxyVoteRequest(string DateOptionId, string ParticipantId);
    
    public static IEndpointRouteBuilder MapDateEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/trips/{tripId:guid}/date-votes",
                async (Guid tripId, DateSelfVoteRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    if (!Guid.TryParse(req.DateOptionId, out var dateOptionId))
                        return Results.BadRequest("Invalid DateOptionId.");

                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var participantId = await db.Participants
                        .Where(p => p.TripId == tripId && p.UserId == me)
                        .Select(p => p.ParticipantId)
                        .FirstOrDefaultAsync(ct);

                    if (participantId == Guid.Empty) return Results.Forbid();

                    var optExists = await db.DateOptions.AnyAsync(o => o.TripId == tripId && o.DateOptionId == dateOptionId, ct);
                    if (!optExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or option not found"));

                    var dup = await db.DateVotes.AnyAsync(v => v.DateOptionId == dateOptionId && v.ParticipantId == participantId, ct);
                    if (!dup)
                    {
                        db.DateVotes.Add(new DateVoteRecord { DateOptionId = dateOptionId, ParticipantId = participantId });
                        await db.SaveChangesAsync(ct);
                    }

                    return Results.NoContent();
                })
            .WithTags("Dates")
            .WithSummary("Vote self for date option")
            .WithDescription("Current user votes using their participant identity.")
            .Accepts<DateSelfVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest);

        v1.MapPost("/trips/{tripId:guid}/date-votes/proxy",
                async (Guid tripId, DateProxyVoteRequest req, AppDbContext db, CancellationToken ct) =>
                {
                    if (!Guid.TryParse(req.DateOptionId, out var dateOptionId) ||
                        !Guid.TryParse(req.ParticipantId, out var participantId))
                        return Results.BadRequest("Invalid ids.");

                    var placeholder = await db.Participants.FirstOrDefaultAsync(
                        p => p.TripId == tripId && p.ParticipantId == participantId && p.UserId == null, ct);

                    if (placeholder is null) return Results.BadRequest("Only placeholders can be proxied or participant not found.");

                    var optExists = await db.DateOptions.AnyAsync(o => o.TripId == tripId && o.DateOptionId == dateOptionId, ct);
                    if (!optExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or option not found"));

                    var dup = await db.DateVotes.AnyAsync(v => v.DateOptionId == dateOptionId && v.ParticipantId == participantId, ct);
                    if (!dup)
                    {
                        db.DateVotes.Add(new DateVoteRecord { DateOptionId = dateOptionId, ParticipantId = participantId });
                        await db.SaveChangesAsync(ct);
                    }

                    return Results.NoContent();
                })
            .WithTags("Dates")
            .WithSummary("Proxy vote for date option")
            .WithDescription("Cast a vote on behalf of a placeholder participant.")
            .Accepts<DateProxyVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
        
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
            .WithTags("Dates")
            .WithName("ProposeDateOption")
            .WithSummary("Propose date option")
            .WithDescription("Proposes a date (YYYY-MM-DD) for the trip.")
            .Accepts<ProposeDateRequest>("application/json")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);
        return v1;
    }
}