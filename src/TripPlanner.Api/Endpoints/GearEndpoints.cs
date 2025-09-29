using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Gear;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Gear;

namespace TripPlanner.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for managing gear items, grouping, and assignments for trips.
/// </summary>
public static class GearEndpoints
{
    // Internal DTOs to make bulk binding robust in System.Text.Json
    private sealed class BulkCreateGearRequestDto
    {
        public List<BulkGearGroupDto>? Groups { get; set; }
    }
    private sealed class BulkGearGroupDto
    {
        public string? Group { get; set; }
        public List<BulkGearItemDto>? Items { get; set; }
    }
    private sealed class BulkGearItemDto
    {
        public string? Name { get; set; }
        public int Provisioning { get; set; } // 0=EACH, 1=SHARED
        public int? NeededQuantity { get; set; }
        public List<string>? Tags { get; set; }
    }
    private static GearItemDto ToDto(GearItemRecord g)
        => new(
            g.GearId.ToString("D"),
            g.TripId.ToString("D"),
            g.Group,
            g.Name,
            Enum.TryParse<GearProvisioning>(g.Provisioning, out var prov) ? prov : GearProvisioning.EACH,
            g.NeededQuantity,
            g.Tags,
            g.Assignments
                .OrderBy(a => a.CreatedAt)
                .Select(a => new GearAssignmentDto(
                    a.AssignmentId.ToString("D"),
                    a.GearId.ToString("D"),
                    a.ParticipantId.ToString("D"),
                    a.Quantity,
                    a.CreatedAt.UtcDateTime.ToString("O")))
                .ToList());

    public static IEndpointRouteBuilder MapGearEndpoints(this IEndpointRouteBuilder v1)
    {
        // List all gear items for a trip
        v1.MapGet("/trips/{tripId:guid}/gear", async (Guid tripId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                // Only trip members can read
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();
                var isMember = await db.Participants.AnyAsync(p => p.TripId == tripId && p.UserId == me, ct);
                if (!isMember) return Results.Forbid();

                var items = await db.GearItems
                    .AsNoTracking()
                    .Include(g => g.Assignments)
                    .Where(g => g.TripId == tripId)
                    .OrderBy(g => g.Group).ThenBy(g => g.Name)
                    .ToListAsync(ct);
                return Results.Ok(items.Select(ToDto).ToList());
            })
            .WithTags("Gear")
            .WithSummary("List gear items")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Create one gear item
        v1.MapPost("/trips/{tripId:guid}/gear", async (Guid tripId, CreateGearItemRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var trip = await db.Trips.Include(t => t.Participants).FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                // Only organizer can create items (no editors role modeled yet)
                if (trip.OrganizerId != me) return Results.Forbid();

                var item = new GearItemRecord
                {
                    GearId = Guid.NewGuid(),
                    TripId = tripId,
                    Group = req.Group?.Trim() ?? string.Empty,
                    Name = req.Name?.Trim() ?? string.Empty,
                    Provisioning = (req.Provisioning).ToString(),
                    NeededQuantity = req.NeededQuantity,
                };
                if (item.Provisioning == nameof(GearProvisioning.EACH))
                {
                    // NeededQuantity not meaningful for EACH, keep null
                    item.NeededQuantity = null;
                }
                item.Tags = (req.Tags ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray();

                if (string.IsNullOrWhiteSpace(item.Group) || string.IsNullOrWhiteSpace(item.Name))
                    return Results.BadRequest("Group and Name are required.");

                db.GearItems.Add(item);
                await db.SaveChangesAsync(ct);

                var created = await db.GearItems.Include(g => g.Assignments).FirstAsync(g => g.GearId == item.GearId, ct);
                return Results.Created($"/api/v1/trips/{tripId}/gear/{item.GearId:D}", ToDto(created));
            })
            .WithTags("Gear")
            .WithSummary("Create gear item")
            .Accepts<CreateGearItemRequest>("application/json")
            .Produces<GearItemDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        // Update gear item
        v1.MapPut("/trips/{tripId:guid}/gear/{gearId:guid}", async (Guid tripId, Guid gearId, UpdateGearItemRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var item = await db.GearItems.Include(g => g.Assignments).FirstOrDefaultAsync(g => g.TripId == tripId && g.GearId == gearId, ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or gear not found"));

                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                if (organizerId == Guid.Empty || organizerId != me) return Results.Forbid();

                if (req.Group is not null) item.Group = req.Group.Trim();
                if (req.Name is not null) item.Name = req.Name.Trim();
                if (req.Provisioning.HasValue) item.Provisioning = req.Provisioning.Value.ToString();
                if (req.Tags is not null) item.Tags = req.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray();

                if (string.Equals(item.Provisioning, nameof(GearProvisioning.EACH), StringComparison.OrdinalIgnoreCase))
                    item.NeededQuantity = null;
                else
                    item.NeededQuantity = req.NeededQuantity;

                await db.SaveChangesAsync(ct);
                var fresh = await db.GearItems.Include(g => g.Assignments).FirstAsync(g => g.GearId == item.GearId, ct);
                return Results.Ok(ToDto(fresh));
            })
            .WithTags("Gear")
            .WithSummary("Update gear item")
            .Accepts<UpdateGearItemRequest>("application/json")
            .Produces<GearItemDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // Delete gear item
        v1.MapDelete("/trips/{tripId:guid}/gear/{gearId:guid}", async (Guid tripId, Guid gearId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var item = await db.GearItems.FirstOrDefaultAsync(g => g.TripId == tripId && g.GearId == gearId, ct);
                if (item is null) return Results.NoContent();

                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                if (organizerId == Guid.Empty || organizerId != me) return Results.Forbid();

                db.GearItems.Remove(item);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Gear")
            .WithSummary("Delete gear item")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Create assignment (claim)
        v1.MapPost("/trips/{tripId:guid}/gear/{gearId:guid}/assignments", async (Guid tripId, Guid gearId, CreateGearAssignmentRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var item = await db.GearItems.Include(g => g.Assignments).FirstOrDefaultAsync(g => g.TripId == tripId && g.GearId == gearId, ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or gear not found"));

                if (!Guid.TryParse(req.ParticipantId, out var participantId))
                    return Results.BadRequest("Invalid participantId");

                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                var participant = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == participantId, ct);
                if (participant is null) return Results.BadRequest("Participant not part of trip");

                var isOrganizer = organizerId != Guid.Empty && organizerId == me;
                var isSelf = participant.UserId == me;
                var isPlaceholder = participant.UserId == null;
                if (!(isOrganizer || isSelf || (isOrganizer && isPlaceholder))) return Results.Forbid();

                var q = req.Quantity.GetValueOrDefault(1);
                if (q < 1) return Results.BadRequest("Quantity must be >= 1");

                var assign = new GearAssignmentRecord
                {
                    AssignmentId = Guid.NewGuid(),
                    GearId = gearId,
                    ParticipantId = participantId,
                    Quantity = q,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.GearAssignments.Add(assign);
                await db.SaveChangesAsync(ct);

                var fresh = await db.GearItems.Include(g => g.Assignments).FirstAsync(g => g.GearId == item.GearId, ct);
                return Results.Ok(ToDto(fresh));
            })
            .WithTags("Gear")
            .WithSummary("Create gear assignment (claim)")
            .Accepts<CreateGearAssignmentRequest>("application/json")
            .Produces<GearItemDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Update assignment quantity
        v1.MapPut("/trips/{tripId:guid}/gear/{gearId:guid}/assignments/{assignmentId:guid}", async (Guid tripId, Guid gearId, Guid assignmentId, CreateGearAssignmentRequest req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var assignment = await db.GearAssignments.Include(a => a.Gear).FirstOrDefaultAsync(a => a.GearId == gearId && a.AssignmentId == assignmentId, ct);
                if (assignment is null || assignment.Gear?.TripId != tripId)
                    return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or gear or assignment not found"));

                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                var participant = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == assignment.ParticipantId, ct);
                if (participant is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Participant not found"));
                var isOrganizer = organizerId != Guid.Empty && organizerId == me;
                var isSelf = participant.UserId == me;
                if (!(isOrganizer || isSelf)) return Results.Forbid();

                var q = req.Quantity.GetValueOrDefault(1);
                if (q < 1) return Results.BadRequest("Quantity must be >= 1");

                assignment.Quantity = q;
                await db.SaveChangesAsync(ct);

                var fresh = await db.GearItems.Include(g => g.Assignments).FirstAsync(g => g.GearId == gearId, ct);
                return Results.Ok(ToDto(fresh));
            })
            .WithTags("Gear")
            .WithSummary("Update gear assignment quantity")
            .Accepts<CreateGearAssignmentRequest>("application/json")
            .Produces<GearItemDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Delete assignment
        v1.MapDelete("/trips/{tripId:guid}/gear/{gearId:guid}/assignments/{assignmentId:guid}", async (Guid tripId, Guid gearId, Guid assignmentId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var assignment = await db.GearAssignments.Include(a => a.Gear).FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.GearId == gearId, ct);
                if (assignment is null || assignment.Gear?.TripId != tripId) return Results.NoContent();

                var organizerId = await db.Trips.Where(t => t.TripId == tripId).Select(t => t.OrganizerId).FirstOrDefaultAsync(ct);
                var participant = await db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == assignment.ParticipantId, ct);
                if (participant is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Participant not found"));
                var isOrganizer = organizerId != Guid.Empty && organizerId == me;
                var isSelf = participant.UserId == me;
                if (!(isOrganizer || isSelf)) return Results.Forbid();

                db.GearAssignments.Remove(assignment);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Gear")
            .WithSummary("Delete gear assignment (unclaim)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Bulk add
        v1.MapPost("/trips/{tripId:guid}/gear/bulk", async (Guid tripId, BulkCreateGearRequestDto req, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var isMember = await db.Participants.AnyAsync(p => p.TripId == tripId && p.UserId == me, ct);
                if (!isMember) return Results.Forbid();

                var created = new List<GearItemRecord>();
                foreach (var group in req.Groups ?? new List<BulkGearGroupDto>())
                {
                    var groupName = group.Group?.Trim();
                    if (string.IsNullOrWhiteSpace(groupName)) continue;
                    foreach (var item in group.Items ?? new List<BulkGearItemDto>())
                    {
                        var name = item.Name?.Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var prov = item.Provisioning == 1 ? GearProvisioning.SHARED : GearProvisioning.EACH;
                        var rec = new GearItemRecord
                        {
                            GearId = Guid.NewGuid(),
                            TripId = tripId,
                            Group = groupName!,
                            Name = name!,
                            Provisioning = prov.ToString(),
                            NeededQuantity = prov == GearProvisioning.EACH ? null : item.NeededQuantity,
                        };
                        rec.Tags = (item.Tags ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray();
                        created.Add(rec);
                    }
                }

                if (created.Count == 0)
                {
                    // Be lenient: return empty list instead of 400 so clients can post messy input without failing the flow
                    return Results.Ok(Array.Empty<GearItemDto>());
                }

                await db.GearItems.AddRangeAsync(created, ct);
                await db.SaveChangesAsync(ct);

                // Return created items with assignments (empty)
                var ids = created.Select(c => c.GearId).ToArray();
                var items = await db.GearItems.Include(g => g.Assignments).Where(g => ids.Contains(g.GearId)).ToListAsync(ct);
                return Results.Ok(items.Select(ToDto).ToList());
            })
            .WithTags("Gear")
            .WithSummary("Bulk create gear items")
            .Accepts<BulkCreateGearRequest>("application/json")
            .Produces<IReadOnlyList<GearItemDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        return v1;
    }
}
