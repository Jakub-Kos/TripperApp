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
        v1.MapPost("/trips/{tripId:guid}/term-proposals", async (Guid tripId, ProposeTermRequest req, AppDbContext db, CancellationToken ct) =>
            {
                if (!DateOnly.TryParse(req.Start, out var start) || !DateOnly.TryParse(req.End, out var end))
                    return Results.BadRequest("Invalid date format. Use YYYY-MM-DD.");
                if (end < start) return Results.BadRequest("End must be on or after start.");

                var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                var entity = new TermProposalRecord
                {
                    TermProposalId = Guid.NewGuid(),
                    TripId = tripId,
                    StartIso = start.ToString("yyyy-MM-dd"),
                    EndIso = end.ToString("yyyy-MM-dd")
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
            .Produces(StatusCodes.Status400BadRequest);

        return v1;
    }
}