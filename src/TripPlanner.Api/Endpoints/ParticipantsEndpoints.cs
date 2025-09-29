using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for managing trip participants and placeholder claims.
/// </summary>
public static class ParticipantsEndpoints
{
    // Request contracts used by the endpoints
    public record CreatePlaceholderRequest(string DisplayName);
    public record IssueClaimCodeRequest(int? ExpiresInMinutes);
    public record ClaimPlaceholderRequest(string Code, string? DisplayName);
    public record UpdateParticipantDisplayNameRequest(string DisplayName);
    
    /// <summary>
    /// Registers participant-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapParticipantsEndpoints(this IEndpointRouteBuilder v1)
    {
        // List participants (including placeholders)
        v1.MapGet("/trips/{tripId:guid}/participants", async (Guid tripId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                Guid.TryParse(sub, out var me);

                var trip = await db.Trips
                    .Include(t => t.Participants)
                        .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                var organizerId = trip.OrganizerId;
                var list = trip.Participants.Select(p => new TripPlanner.Core.Contracts.Contracts.Common.Participants.ParticipantInfoDto
                {
                    ParticipantId = p.ParticipantId.ToString(),
                    DisplayName = p.DisplayName,
                    IsPlaceholder = p.IsPlaceholder,
                    IsOrganizer = p.UserId != null && p.UserId == organizerId,
                    IsMe = p.UserId != null && me != Guid.Empty && p.UserId == me,
                    UserId = p.UserId?.ToString(),
                    Username = p.User?.Email
                }).ToList();
                return Results.Ok(list);
            })
            .WithTags("Participants")
            .WithSummary("List trip participants")
            .WithDescription("Returns all participants for the trip, including placeholders.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Update displayName of a placeholder participant (organizer only)
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

                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                if (organizerId != me) return Results.Forbid();

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

                    var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                    if (organizerId == Guid.Empty) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                    if (organizerId != me) return Results.Forbid();

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

                var existingForUser = await db.Participants
                    .FirstOrDefaultAsync(p => p.TripId == claim.TripId && p.UserId == me, ct);

                if (existingForUser is not null && existingForUser.ParticipantId != placeholder.ParticipantId)
                {
                    return Results.BadRequest("You already have a participant in this trip. Cannot claim another.");
                }

                placeholder.UserId = me;
                placeholder.IsPlaceholder = false;
                placeholder.ClaimedAt = now;
                if (!string.IsNullOrWhiteSpace(req.DisplayName))
                    placeholder.DisplayName = req.DisplayName.Trim();

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
        
        // New: current user can update their own (non-placeholder) display name within a trip
        v1.MapPatch("/trips/{tripId:guid}/participants/me", async (Guid tripId, UpdateParticipantDisplayNameRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();
                var display = (req.DisplayName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(display)) return Results.BadRequest("DisplayName is required.");

                var participant = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.UserId == me, ct);
                if (participant is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Participant not found"));
                if (participant.IsPlaceholder || participant.UserId == null)
                    return Results.BadRequest("Only real users can rename themselves via this endpoint.");

                participant.DisplayName = display;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Participants")
            .WithSummary("Update my trip display name")
            .WithDescription("Allows the authenticated user to change their own display name within the specified trip.")
            .Accepts<UpdateParticipantDisplayNameRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        
        // New: Claim a placeholder by selecting it (no organizer code), as long as the user doesn't already have a participant in the trip
        v1.MapPost("/trips/{tripId:guid}/placeholders/{participantId:guid}/claim", async (Guid tripId, Guid participantId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                // Ensure trip exists
                var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, ct);
                if (!tripExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                // Ensure user does not already have a participant in this trip
                var existingForUser = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.UserId == me, ct);
                if (existingForUser is not null)
                    return Results.BadRequest("You already have a participant in this trip. Cannot claim another.");

                var placeholder = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == participantId, ct);
                if (placeholder is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Placeholder not found"));
                if (!placeholder.IsPlaceholder || placeholder.UserId != null)
                    return Results.BadRequest("Selected participant is not a claimable placeholder.");

                placeholder.UserId = me;
                placeholder.IsPlaceholder = false;
                placeholder.ClaimedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Participants")
            .WithSummary("Claim a placeholder by selection")
            .WithDescription("Allows an authenticated user to claim a placeholder in the trip without a one-time code, provided they don't already have a participant in the trip.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        
        return v1;
    }
}