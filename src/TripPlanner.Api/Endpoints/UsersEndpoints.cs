using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;

namespace TripPlanner.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder v1)
    {
        // Self-delete current user. Intended for test cleanup/dev use.
        v1.MapDelete("/users/me", async (AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                // Remove participant rows for this user to avoid FK restriction
                var participants = await db.Participants.Where(p => p.UserId == me).ToListAsync(ct);
                if (participants.Count > 0)
                {
                    db.Participants.RemoveRange(participants);
                    await db.SaveChangesAsync(ct);
                }

                var rec = await db.Users.FirstOrDefaultAsync(u => u.UserId == me, ct);
                if (rec is null) return Results.NoContent();

                db.Users.Remove(rec); // cascades will remove refresh tokens
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Users")
            .WithSummary("Delete current user")
            .WithDescription("Deletes the authenticated user and related data (participants, refresh tokens). Useful for test cleanup.")
            .Produces(StatusCodes.Status204NoContent);

        return v1;
    }
}
