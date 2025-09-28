using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Api.Infrastructure.Validation;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Api.Endpoints;

public static class TermEndpoints
{
    public record ProposeTermRequest(string Start, string End); // YYYY-MM-DD

    public static IEndpointRouteBuilder MapTermEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/trips/{tripId:guid}/term-proposals", async (Guid tripId, ProposeTermRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!DateOnly.TryParse(req.Start, out var start) || !DateOnly.TryParse(req.End, out var end))
                    return Results.BadRequest("Invalid date format. Use YYYY-MM-DD.");
                if (end < start) return Results.BadRequest("End must be on or after start.");

                var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                // Require current user to be a participant of the trip
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();
                var isParticipant = await db.Participants.AnyAsync(p => p.TripId == tripId && p.UserId == me, ct);
                if (!isParticipant) return Results.Forbid();

                var entity = new TermProposalRecord
                {
                    TermProposalId = Guid.NewGuid(),
                    TripId = tripId,
                    StartIso = start.ToString("yyyy-MM-dd"),
                    EndIso = end.ToString("yyyy-MM-dd"),
                    CreatedByUserId = me,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                db.TermProposals.Add(entity);
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/v1/trips/{tripId}/term-proposals/{entity.TermProposalId}", new { termProposalId = entity.TermProposalId });
            })
            .WithTags("Dates")
            .WithSummary("Propose a term (start/end) for the trip")
            .WithDescription("Adds a proposed time window when the trip could take place. Accepts YYYY-MM-DD for start and end.")
            .Accepts<ProposeTermRequest>("application/json")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        // Vote on a term proposal (self)
        v1.MapPost("/trips/{tripId:guid}/term-proposals/{termId:guid}/votes", async (Guid tripId, Guid termId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                // Verify trip & term
                var term = await db.TermProposals.FirstOrDefaultAsync(t => t.TermProposalId == termId && t.TripId == tripId, ct);
                if (term is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Term proposal not found"));

                // Find participant row for current user
                var participantId = await db.Participants
                    .Where(p => p.TripId == tripId && p.UserId == me)
                    .Select(p => p.ParticipantId)
                    .FirstOrDefaultAsync(ct);
                if (participantId == Guid.Empty) return Results.Forbid();

                var exists = await db.TermProposalVotes.AnyAsync(v => v.TermProposalId == termId && v.ParticipantId == participantId, ct);
                if (!exists)
                {
                    db.TermProposalVotes.Add(new TermProposalVoteRecord
                    {
                        TermProposalId = termId,
                        ParticipantId = participantId
                    });
                    await db.SaveChangesAsync(ct);
                }
                return Results.NoContent();
            })
            .WithTags("Dates")
            .WithSummary("Vote for a term proposal")
            .WithDescription("Current user (participant) votes for a specific term proposal.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        // Remove a vote on a term proposal (self)
        v1.MapDelete("/trips/{tripId:guid}/term-proposals/{termId:guid}/votes", async (Guid tripId, Guid termId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var term = await db.TermProposals.FirstOrDefaultAsync(t => t.TermProposalId == termId && t.TripId == tripId, ct);
                if (term is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Term proposal not found"));

                var participantId = await db.Participants
                    .Where(p => p.TripId == tripId && p.UserId == me)
                    .Select(p => p.ParticipantId)
                    .FirstOrDefaultAsync(ct);
                if (participantId == Guid.Empty) return Results.Forbid();

                var vote = await db.TermProposalVotes.FirstOrDefaultAsync(v => v.TermProposalId == termId && v.ParticipantId == participantId, ct);
                if (vote is not null)
                {
                    db.TermProposalVotes.Remove(vote);
                    await db.SaveChangesAsync(ct);
                }
                return Results.NoContent();
            })
            .WithTags("Dates")
            .WithSummary("Remove vote for a term proposal")
            .WithDescription("Current user (participant) removes their vote for a specific term proposal. Idempotent: returns 204 even if no vote existed.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        // Delete a term proposal (creator or organizer)
        v1.MapDelete("/trips/{tripId:guid}/term-proposals/{termId:guid}", async (Guid tripId, Guid termId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var term = await db.TermProposals.Include(t => t.Trip).FirstOrDefaultAsync(t => t.TermProposalId == termId && t.TripId == tripId, ct);
                if (term is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Term proposal not found"));

                var organizerId = term.Trip.OrganizerId;
                if (term.CreatedByUserId != me && organizerId != me)
                    return Results.Forbid();

                db.TermProposals.Remove(term);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Dates")
            .WithSummary("Delete a term proposal")
            .WithDescription("Deletes a term proposal. Only the creator or the trip organizer can delete it.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        return v1;
    }
}