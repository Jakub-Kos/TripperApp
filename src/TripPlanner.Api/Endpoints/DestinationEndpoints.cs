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

        // GET destination detail including images and voters
        v1.MapGet("/trips/{tripId:guid}/destinations/{destinationId:guid}",
                async (Guid tripId, Guid destinationId, AppDbContext db, CancellationToken ct) =>
                {
                    var dest = await db.Destinations
                        .AsNoTracking()
                        .Include(d => d.Images)
                        .Include(d => d.Votes)
                        .FirstOrDefaultAsync(d => d.TripId == tripId && d.DestinationId == destinationId, ct);
                    if (dest is null)
                        return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    var dto = new
                    {
                        destinationId = dest.DestinationId,
                        title = dest.Title,
                        description = dest.Description,
                        imageUrls = dest.Images.Select(i => i.Url).ToArray(),
                        createdByUserId = dest.CreatedByUserId,
                        createdAt = dest.CreatedAt,
                        voters = dest.Votes.OrderBy(v => v.ParticipantId).Select(v => v.ParticipantId.ToString("D")).ToArray(),
                        votesCount = dest.Votes.Count
                    };
                    return Results.Ok(dto);
                })
            .WithTags("Destinations")
            .WithSummary("Get destination proposal detail")
            .WithDescription("Returns destination detail including image URLs and participantIds of voters.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/{tripId}/destinations",
                async (string tripId, ProposeDestinationRequest req, System.Security.Claims.ClaimsPrincipal user, ProposeDestinationHandler handler, AppDbContext db, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var id = await handler.Handle(new ProposeDestinationCommand(tripId, req.Title, req.Description, req.ImageUrls), ct);
                    if (id is null)
                        return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                    // Set creator metadata if not set yet
                    if (Guid.TryParse(tripId, out var tripGuid))
                    {
                        var dest = await db.Destinations.FirstOrDefaultAsync(d => d.TripId == tripGuid && d.DestinationId == id.Value.Value, ct);
                        if (dest is not null)
                        {
                            // Only set on first creation
                            if (dest.CreatedByUserId == Guid.Empty || dest.CreatedAt == default)
                            {
                                dest.CreatedByUserId = me;
                                dest.CreatedAt = DateTimeOffset.UtcNow;
                                await db.SaveChangesAsync(ct);
                            }
                        }
                    }

                    return Results.Created($"/api/v1/trips/{tripId}/destinations/{id.Value.Value:D}", new { destinationId = id.Value.Value.ToString("D") });
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
        
        // Upload images to a destination proposal (png/jpeg), max 10 per destination
        v1.MapPost("/trips/{tripId:guid}/destinations/{destinationId:guid}/images",
                async (Guid tripId,
                       Guid destinationId,
                       HttpRequest request,
                       IWebHostEnvironment env,
                       AppDbContext db,
                       CancellationToken ct) =>
                {
                    // Validate trip and destination
                    var dest = await db.Destinations.Include(d => d.Images)
                        .FirstOrDefaultAsync(d => d.TripId == tripId && d.DestinationId == destinationId, ct);
                    if (dest is null)
                        return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    if (!request.HasFormContentType)
                        return Results.BadRequest("Expected multipart/form-data with files.");

                    var form = await request.ReadFormAsync(ct);
                    var files = form.Files;
                    if (files is null || files.Count == 0)
                        return Results.BadRequest("No files uploaded.");

                    // Allowed content types
                    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png" };

                    // Enforce limit: total images per destination <= 10
                    var existingCount = dest.Images.Count;
                    if (existingCount >= 10)
                        return Results.BadRequest("This destination already has the maximum of 10 images.");

                    var toProcess = files.Count;
                    if (existingCount + toProcess > 10)
                        return Results.BadRequest($"Too many images. You can upload at most {10 - existingCount} more.");

                    // Ensure destination folder exists
                    var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                    var relativeDir = Path.Combine("uploads", "destinations", destinationId.ToString("D"));
                    var absDir = Path.Combine(root, relativeDir);
                    Directory.CreateDirectory(absDir);

                    var savedUrls = new List<string>();

                    foreach (var file in files)
                    {
                        if (file.Length == 0) continue;
                        if (!allowed.Contains(file.ContentType))
                            return Results.BadRequest("Unsupported file type. Only image/jpeg and image/png are allowed.");

                        var ext = Path.GetExtension(file.FileName);
                        // Normalize extension by content type if missing or odd
                        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5)
                            ext = file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                        else
                        {
                            // Sanitize to .png/.jpg
                            var lower = ext.ToLowerInvariant();
                            if (lower is not (".png" or ".jpg" or ".jpeg"))
                                ext = file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                            else if (lower == ".jpeg") ext = ".jpg";
                        }

                        var name = $"{Guid.NewGuid():N}{ext}";
                        var absPath = Path.Combine(absDir, name);
                        await using (var stream = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write))
                        {
                            await file.CopyToAsync(stream, ct);
                        }

                        var urlPath = "/" + Path.Combine(relativeDir, name).Replace('\\', '/');
                        // Persist DB record
                        db.DestinationImages.Add(new DestinationImageRecord
                        {
                            DestinationId = destinationId,
                            Url = urlPath
                        });
                        savedUrls.Add(urlPath);
                    }

                    await db.SaveChangesAsync(ct);

                    return Results.Created($"/api/v1/trips/{tripId}/destinations/{destinationId}/images", new { urls = savedUrls });
                })
            .WithTags("Destinations")
            .WithSummary("Upload images for a destination proposal")
            .WithDescription("Accepts multipart/form-data with PNG/JPEG files. Up to 10 images per destination proposal. Returns URLs of stored images.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        // List images for a destination (returns id + url)
        v1.MapGet("/trips/{tripId:guid}/destinations/{destinationId:guid}/images",
                async (Guid tripId, Guid destinationId, AppDbContext db, CancellationToken ct) =>
                {
                    var dest = await db.Destinations.Include(d => d.Images)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.TripId == tripId && d.DestinationId == destinationId, ct);
                    if (dest is null)
                        return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    var images = dest.Images
                        .OrderBy(i => i.Id)
                        .Select(i => new { id = i.Id, url = i.Url })
                        .ToList();
                    return Results.Ok(images);
                })
            .WithTags("Destinations")
            .WithSummary("List images for a destination proposal")
            .WithDescription("Returns the images (id and url) stored for a destination proposal.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Delete a destination proposal (creator or organizer)
        v1.MapDelete("/trips/{tripId:guid}/destinations/{destinationId:guid}",
                async (Guid tripId, Guid destinationId, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var dest = await db.Destinations
                        .Include(d => d.Trip)
                        .FirstOrDefaultAsync(d => d.TripId == tripId && d.DestinationId == destinationId, ct);
                    if (dest is null)
                        return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    if (dest.CreatedByUserId != me && dest.Trip.OrganizerId != me)
                        return Results.Forbid();

                    db.Destinations.Remove(dest);
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                })
            .WithTags("Destinations")
            .WithSummary("Delete a destination proposal")
            .WithDescription("Deletes a destination proposal. Only the creator or the trip organizer can delete it.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        // Delete a specific image (organizer only); idempotent
        v1.MapDelete("/trips/{tripId:guid}/destinations/{destinationId:guid}/images/{imageId:int}",
                async (Guid tripId, Guid destinationId, int imageId, AppDbContext db, IWebHostEnvironment env, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
                {
                    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                    if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                    var dest = await db.Destinations.Include(d => d.Trip)
                        .Include(d => d.Images)
                        .FirstOrDefaultAsync(d => d.TripId == tripId && d.DestinationId == destinationId, ct);
                    if (dest is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or destination not found"));

                    if (dest.Trip.OrganizerId != me) return Results.Forbid();

                    var img = dest.Images.FirstOrDefault(i => i.Id == imageId);
                    if (img is null) return Results.NoContent();

                    // Try delete the file from disk
                    var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                    try
                    {
                        var url = img.Url?.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            var absPath = Path.Combine(root, url);
                            if (File.Exists(absPath))
                            {
                                File.Delete(absPath);
                            }
                        }
                    }
                    catch { /* ignore file IO errors during deletion */ }

                    db.DestinationImages.Remove(img);
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                })
            .WithTags("Destinations")
            .WithSummary("Delete an image from a destination proposal")
            .WithDescription("Organizer can delete a stored image by its id. Idempotent: returns 204 if already removed.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);
        
        return v1;
    }
}