using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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
    public record DateSelfVoteRequest(string Date); // YYYY-MM-DD
    public record DateProxyVoteRequest(string Date, string ParticipantId);
    public record SetDateRangeRequest(string Start, string End); // YYYY-MM-DD
    
    public static IEndpointRouteBuilder MapDateEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPut("/trips/{tripId:guid}/date-range",
                async (Guid tripId, SetDateRangeRequest req, AppDbContext db, CancellationToken ct) =>
                {
                    if (!DateOnly.TryParse(req.Start, out var start) || !DateOnly.TryParse(req.End, out var end))
                        return Results.BadRequest("Invalid date format. Use YYYY-MM-DD.");
                    if (end < start) return Results.BadRequest("End must be on or after start.");

                    var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                    if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                    trip.StartDate = start;
                    trip.EndDate = end;
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                })
            .WithTags("Dates")
            .WithSummary("Set trip date range")
            .WithDescription("Defines the inclusive date range during which the trip should take place.")
            .Accepts<SetDateRangeRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        v1.MapPost("/trips/{tripId:guid}/date-votes",
                async (Guid tripId, DateSelfVoteRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    if (!DateOnly.TryParse(req.Date, out var date))
                        return Results.BadRequest("Invalid date format. Use YYYY-MM-DD.");

                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var participantId = await db.Participants
                        .Where(p => p.TripId == tripId && p.UserId == me)
                        .Select(p => p.ParticipantId)
                        .FirstOrDefaultAsync(ct);

                    if (participantId == Guid.Empty) return Results.Forbid();

                    var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                    if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                    if (trip.StartDate is not null && trip.EndDate is not null && (date < trip.StartDate || date > trip.EndDate))
                        return Results.BadRequest("Date is outside the defined range.");

                    // find or create date option for this date
                    var dateIso = date.ToString("yyyy-MM-dd");
                    var opt = await db.DateOptions.FirstOrDefaultAsync(o => o.TripId == tripId && o.DateIso == dateIso, ct);
                    if (opt is null)
                    {
                        opt = new DateOptionRecord { DateOptionId = Guid.NewGuid(), TripId = tripId, DateIso = dateIso };
                        db.DateOptions.Add(opt);
                        await db.SaveChangesAsync(ct);
                    }

                    var dup = await db.DateVotes.AnyAsync(v => v.DateOptionId == opt.DateOptionId && v.ParticipantId == participantId, ct);
                    if (!dup)
                    {
                        db.DateVotes.Add(new DateVoteRecord { DateOptionId = opt.DateOptionId, ParticipantId = participantId });
                        await db.SaveChangesAsync(ct);
                    }

                    return Results.NoContent();
                })
            .WithTags("Dates")
            .WithSummary("Vote self for a specific date within the range")
            .WithDescription("Current user votes for a specific date (YYYY-MM-DD). If the date option doesn't exist, it's created.")
            .Accepts<DateSelfVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest);

        v1.MapPost("/trips/{tripId:guid}/date-votes/proxy",
                async (Guid tripId, DateProxyVoteRequest req, AppDbContext db, CancellationToken ct) =>
                {
                    if (!DateOnly.TryParse(req.Date, out var date) ||
                        !Guid.TryParse(req.ParticipantId, out var participantId))
                        return Results.BadRequest("Invalid inputs.");

                    var placeholder = await db.Participants.FirstOrDefaultAsync(
                        p => p.TripId == tripId && p.ParticipantId == participantId && p.UserId == null, ct);

                    if (placeholder is null) return Results.BadRequest("Only placeholders can be proxied or participant not found.");

                    var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                    if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                    if (trip.StartDate is not null && trip.EndDate is not null && (date < trip.StartDate || date > trip.EndDate))
                        return Results.BadRequest("Date is outside the defined range.");

                    var dateIso = date.ToString("yyyy-MM-dd");
                    var opt = await db.DateOptions.FirstOrDefaultAsync(o => o.TripId == tripId && o.DateIso == dateIso, ct);
                    if (opt is null)
                    {
                        opt = new DateOptionRecord { DateOptionId = Guid.NewGuid(), TripId = tripId, DateIso = dateIso };
                        db.DateOptions.Add(opt);
                        await db.SaveChangesAsync(ct);
                    }

                    var dup = await db.DateVotes.AnyAsync(v => v.DateOptionId == opt.DateOptionId && v.ParticipantId == participantId, ct);
                    if (!dup)
                    {
                        db.DateVotes.Add(new DateVoteRecord { DateOptionId = opt.DateOptionId, ParticipantId = participantId });
                        await db.SaveChangesAsync(ct);
                    }

                    return Results.NoContent();
                })
            .WithTags("Dates")
            .WithSummary("Proxy vote for specific date")
            .WithDescription("Cast a vote on behalf of a placeholder participant for a specific date (YYYY-MM-DD).")
            .Accepts<DateProxyVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
        
        v1.MapPost("/trips/{tripId}/date-options",
                () => Results.StatusCode(StatusCodes.Status410Gone))
            .WithTags("Dates")
            .WithName("ProposeDateOption")
            .WithSummary("Deprecated: propose date option")
            .WithDescription("Deprecated. Use PUT /trips/{tripId}/date-range and POST /trips/{tripId}/date-votes with a date instead.")
            .Produces(StatusCodes.Status410Gone);

        // DELETE self vote for a specific date
        v1.MapDelete("/trips/{tripId:guid}/date-votes", async (Guid tripId, [FromBody] DateSelfVoteRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!DateOnly.TryParse(req.Date, out var date))
                    return Results.BadRequest("Invalid date format. Use YYYY-MM-DD.");

                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var participantId = await db.Participants
                    .Where(p => p.TripId == tripId && p.UserId == me)
                    .Select(p => p.ParticipantId)
                    .FirstOrDefaultAsync(ct);

                if (participantId == Guid.Empty) return Results.Forbid();

                var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                var dateIso = date.ToString("yyyy-MM-dd");
                var opt = await db.DateOptions.FirstOrDefaultAsync(o => o.TripId == tripId && o.DateIso == dateIso, ct);
                if (opt is null)
                {
                    // Nothing to delete
                    return Results.NoContent();
                }

                var vote = await db.DateVotes.FirstOrDefaultAsync(v => v.DateOptionId == opt.DateOptionId && v.ParticipantId == participantId, ct);
                if (vote is not null)
                {
                    db.DateVotes.Remove(vote);
                    await db.SaveChangesAsync(ct);
                }

                return Results.NoContent();
            })
            .WithTags("Dates")
            .WithSummary("Remove self vote for a specific date")
            .WithDescription("Current user removes their vote for a specific date (YYYY-MM-DD). Idempotent: returns 204 even if not voted.")
            .Accepts<DateSelfVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        // DELETE proxy vote for a specific date (placeholder participant)
        v1.MapDelete("/trips/{tripId:guid}/date-votes/proxy", async (Guid tripId, [FromBody] DateProxyVoteRequest req, AppDbContext db, CancellationToken ct) =>
            {
                if (!DateOnly.TryParse(req.Date, out var date) || !Guid.TryParse(req.ParticipantId, out var participantId))
                    return Results.BadRequest("Invalid inputs.");

                var placeholder = await db.Participants.FirstOrDefaultAsync(
                    p => p.TripId == tripId && p.ParticipantId == participantId && p.UserId == null, ct);
                if (placeholder is null) return Results.BadRequest("Only placeholders can be proxied or participant not found.");

                var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                var dateIso = date.ToString("yyyy-MM-dd");
                var opt = await db.DateOptions.FirstOrDefaultAsync(o => o.TripId == tripId && o.DateIso == dateIso, ct);
                if (opt is null) return Results.NoContent();

                var vote = await db.DateVotes.FirstOrDefaultAsync(v => v.DateOptionId == opt.DateOptionId && v.ParticipantId == participantId, ct);
                if (vote is not null)
                {
                    db.DateVotes.Remove(vote);
                    await db.SaveChangesAsync(ct);
                }
                return Results.NoContent();
            })
            .WithTags("Dates")
            .WithSummary("Proxy remove vote for a specific date")
            .WithDescription("Remove a vote on behalf of a placeholder participant for a specific date (YYYY-MM-DD). Idempotent: returns 204 even if not voted.")
            .Accepts<DateProxyVoteRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
        
        return v1;
    }
}