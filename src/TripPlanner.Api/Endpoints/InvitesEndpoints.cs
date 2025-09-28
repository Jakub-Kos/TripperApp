using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Api.Endpoints;

public static class InvitesEndpoints
{
    public record CreateInviteRequest(int? ExpiresInMinutes, int? MaxUses);
    public record JoinTripRequest(string Code);
    
    public static IEndpointRouteBuilder MapInvitesEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/trips/{tripId:guid}/invites",
                async (Guid tripId, CreateInviteRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                    if (organizerId == Guid.Empty) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                    if (organizerId != me) return Results.Forbid();

                    var code = CodeUtils.GenerateFriendlyCode(10);
                    var hash = CodeUtils.Hash(code);
                    var ttl = TimeSpan.FromMinutes(req.ExpiresInMinutes ?? 1440);
                    var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

                    var rec = new TripInviteRecord
                    {
                        TripId = tripId,
                        CodeHash = hash,
                        ExpiresAt = expiresAt,
                        MaxUses = Math.Max(1, req.MaxUses ?? 10),
                        Uses = 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedByUserId = me
                    };
                    db.TripInvites.Add(rec);
                    await db.SaveChangesAsync(ct);

                    var url = $"app://join/{code}";
                    return Results.Ok(new { inviteId = rec.InviteId, code, url, expiresAt });
                })
            .WithTags("Invites")
            .WithSummary("Create invite code for a trip")
            .WithDescription("Generates a shareable invite code.")
            .Accepts<CreateInviteRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/join",
                async (JoinTripRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var norm = CodeUtils.NormalizeCode(req.Code);
                    var hash = CodeUtils.Hash(norm);
                    var now = DateTimeOffset.UtcNow;

                    var invite = await db.TripInvites
                        .AsTracking()
                        .FirstOrDefaultAsync(i => i.CodeHash == hash, ct);

                    if (invite is null || invite.RevokedAt != null || invite.ExpiresAt <= now)
                        return Results.BadRequest("Invalid or expired code");

                    var already = await db.Participants.AnyAsync(p => p.TripId == invite.TripId && p.UserId == me, ct);
                    if (!already)
                    {
                        var display = await db.Users.Where(u => u.UserId == me).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? "User";
                        db.Participants.Add(new ParticipantRecord
                        {
                            TripId = invite.TripId,
                            ParticipantId = Guid.NewGuid(),
                            UserId = me,
                            IsPlaceholder = false,
                            DisplayName = display,
                            CreatedByUserId = me
                        });
                    }

                    invite.Uses += 1;
                    if (invite.Uses >= invite.MaxUses) invite.RevokedAt = now;

                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                })
            .WithTags("Invites")
            .WithSummary("Join a trip by code")
            .WithDescription("Uses a shareable code to join a trip.")
            .Accepts<JoinTripRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);
        return v1;
    }
}