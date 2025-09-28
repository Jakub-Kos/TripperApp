using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Api.Endpoints;

public static class ParticipantsEndpoints
{
    public record CreatePlaceholderRequest(string DisplayName);
    public record IssueClaimCodeRequest(int? ExpiresInMinutes);
    public record ClaimPlaceholderRequest(string Code, string? DisplayName);
    public record UpdateParticipantDisplayNameRequest(string DisplayName);
    
    public static IEndpointRouteBuilder MapParticipantsEndpoints(this IEndpointRouteBuilder v1)
    {
        // List participants (including placeholders)
        v1.MapGet("/trips/{tripId:guid}/participants", async (Guid tripId, AppDbContext db, CancellationToken ct) =>
            {
                var trip = await db.Trips.Include(t => t.Participants).FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                var list = trip.Participants.Select(p => new TripPlanner.Core.Contracts.Contracts.Common.Participants.ParticipantDto(
                                    p.ParticipantId.ToString(),
                                    p.DisplayName,
                                    p.IsPlaceholder,
                                    p.UserId ?? Guid.Empty)).ToList();
                return Results.Ok(list);
            })
            .WithTags("Participants")
            .WithSummary("List trip participants")
            .WithDescription("Returns all participants for the trip, including placeholders.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Update displayName of a placeholder participant
        v1.MapPatch("/trips/{tripId:guid}/participants/{participantId:guid}", async (Guid tripId, Guid participantId, UpdateParticipantDisplayNameRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();
                var display = (req.DisplayName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(display)) return Results.BadRequest("DisplayName is required.");

                var participant = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == participantId, ct);
                if (participant is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Participant not found"));
                if (!participant.IsPlaceholder || participant.UserId != null)
                    return Results.BadRequest("Only placeholders can be renamed via this endpoint.");

                // must be a participant or the organizer to edit placeholders
                var isTripMember = await db.Participants.AnyAsync(p => p.TripId == tripId && p.UserId == me, ct);
                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                if (!isTripMember && organizerId != me) return Results.Forbid();

                participant.DisplayName = display;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Participants")
            .WithSummary("Update placeholder display name")
            .WithDescription("Allows a trip participant to change a placeholder participant's displayName.")
            .Accepts<UpdateParticipantDisplayNameRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // Delete a participant (organizer only)
        v1.MapDelete("/trips/{tripId:guid}/participants/{participantId:guid}", async (Guid tripId, Guid participantId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var trip = await db.Trips.Include(t => t.Participants).FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                if (trip.OrganizerId != me) return Results.Forbid();

                var participant = trip.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
                if (participant is null) return Results.NoContent();

                // prevent removing the organizer's own participant mapping if exists
                if (participant.UserId == trip.OrganizerId)
                    return Results.BadRequest("Cannot remove organizer from the trip.");

                db.Participants.Remove(participant);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Participants")
            .WithSummary("Remove a participant (organizer only)")
            .WithDescription("Deletes a participant from the trip. Only the organizer can perform this action.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // Existing: Create placeholder
        v1.MapPost("/trips/{tripId:guid}/placeholders",
                async (Guid tripId, CreatePlaceholderRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var exists = await db.Trips.AnyAsync(t => t.TripId == tripId, ct);
                    if (!exists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                    var name = string.IsNullOrWhiteSpace(req.DisplayName) ? "Guest" : req.DisplayName.Trim();
                    var p = new ParticipantRecord
                    {
                        TripId = tripId,
                        ParticipantId = Guid.NewGuid(),
                        UserId = null,
                        IsPlaceholder = true,
                        DisplayName = name,
                        CreatedByUserId = me
                    };
                    db.Participants.Add(p);
                    await db.SaveChangesAsync(ct);

                    return Results.Created($"/api/v1/trips/{tripId}/participants/{p.ParticipantId}", new { participantId = p.ParticipantId });
                })
            .WithTags("Participants")
            .WithSummary("Create a placeholder participant")
            .WithDescription("Adds a named placeholder to the trip.")
            .Accepts<CreatePlaceholderRequest>("application/json")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/{tripId:guid}/placeholders/{participantId:guid}/claim-codes",
                async (Guid tripId, Guid participantId, IssueClaimCodeRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var placeholder = await db.Participants
                        .FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == participantId && p.UserId == null, ct);

                    if (placeholder is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Placeholder not found"));

                    var code = CodeUtils.GenerateFriendlyCode(10);
                    var hash = CodeUtils.Hash(code);
                    var ttl = TimeSpan.FromMinutes(req.ExpiresInMinutes ?? 1440);
                    var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

                    var claim = new PlaceholderClaimRecord
                    {
                        TripId = tripId,
                        ParticipantId = participantId,
                        CodeHash = hash,
                        ExpiresAt = expiresAt,
                        CreatedByUserId = me,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    db.PlaceholderClaims.Add(claim);
                    await db.SaveChangesAsync(ct);

                    var url = $"app://claim/{code}";
                    return Results.Ok(new { code, url, expiresAt });
                })
            .WithTags("Participants")
            .WithSummary("Issue a claim code for a placeholder")
            .WithDescription("Generates a one-time code to let a user claim the placeholder.")
            .Accepts<IssueClaimCodeRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/placeholders/claim",
            async (ClaimPlaceholderRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var norm = CodeUtils.NormalizeCode(req.Code);
                var hash = CodeUtils.Hash(norm);
                var now = DateTimeOffset.UtcNow;

                var claim = await db.PlaceholderClaims
                    .AsTracking()
                    .FirstOrDefaultAsync(c => c.CodeHash == hash, ct);

                if (claim is null || claim.RevokedAt != null || claim.ExpiresAt <= now)
                    return Results.BadRequest("Invalid or expired code");

                var placeholder = await db.Participants
                    .FirstOrDefaultAsync(p => p.TripId == claim.TripId && p.ParticipantId == claim.ParticipantId, ct);

                if (placeholder is null) return Results.BadRequest("Placeholder no longer exists.");

                // If the caller already has a participant in this trip, merge votes into the placeholder participant (keep its ParticipantId).
                var existingForUser = await db.Participants
                    .FirstOrDefaultAsync(p => p.TripId == claim.TripId && p.UserId == me, ct);

                placeholder.UserId = me;
                placeholder.IsPlaceholder = false;
                placeholder.ClaimedAt = now;
                if (!string.IsNullOrWhiteSpace(req.DisplayName))
                    placeholder.DisplayName = req.DisplayName.Trim();

                if (existingForUser is not null && existingForUser.ParticipantId != placeholder.ParticipantId)
                {
                    await db.Database.BeginTransactionAsync(ct);

                    var existingDateVotes = await db.DateVotes
                        .Where(v => v.ParticipantId == existingForUser.ParticipantId)
                        .ToListAsync(ct);

                    foreach (var v in existingDateVotes)
                    {
                        var dup = await db.DateVotes.AnyAsync(d =>
                            d.DateOptionId == v.DateOptionId && d.ParticipantId == placeholder.ParticipantId, ct);
                        if (!dup) { v.ParticipantId = placeholder.ParticipantId; db.DateVotes.Update(v); }
                        else { db.DateVotes.Remove(v); }
                    }

                    var existingDestVotes = await db.DestinationVotes
                        .Where(v => v.ParticipantId == existingForUser.ParticipantId)
                        .ToListAsync(ct);

                    foreach (var v in existingDestVotes)
                    {
                        var dup = await db.DestinationVotes.AnyAsync(d =>
                            d.DestinationId == v.DestinationId && d.ParticipantId == placeholder.ParticipantId, ct);
                        if (!dup) { v.ParticipantId = placeholder.ParticipantId; db.DestinationVotes.Update(v); }
                        else { db.DestinationVotes.Remove(v); }
                    }

                    db.Participants.Remove(existingForUser);
                    await db.SaveChangesAsync(ct);
                    await db.Database.CommitTransactionAsync(ct);
                }

                claim.RevokedAt = now; // one-time
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Participants")
            .WithSummary("Claim a placeholder")
            .WithDescription("Converts a placeholder participant into the current user.")
            .Accepts<ClaimPlaceholderRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);
        
        return v1;
    }
}