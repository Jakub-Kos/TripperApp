using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Transportation;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for managing transportation to the trip start (with documents and routes).
/// </summary>
public static class TransportationEndpoints
{
    // Request contracts used by the endpoints
    public record CreateTransportationRequest(string Title, string? Description);
    public record UpdateTransportationRequest(string Title, string? Description);

    /// <summary>
    /// Registers transportation-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapTransportationEndpoints(this IEndpointRouteBuilder v1)
    {
        // Create transportation item
        v1.MapPost("/trips/{tripId:guid}/transportations", async (Guid tripId, CreateTransportationRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, ct);
                if (!tripExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                var rec = new TransportationRecord
                {
                    TransportationId = Guid.NewGuid(),
                    TripId = tripId,
                    Title = req.Title,
                    Description = req.Description,
                    CreatedByUserId = me,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Transportations.Add(rec);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/v1/trips/{tripId}/transportations/{rec.TransportationId:D}", new { transportationId = rec.TransportationId.ToString("D") });
            })
            .WithTags("Transportations")
            .WithSummary("Create transportation to trip start")
            .Accepts<CreateTransportationRequest>("application/json")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // List transportations
        v1.MapGet("/trips/{tripId:guid}/transportations", async (Guid tripId, AppDbContext db, CancellationToken ct) =>
            {
                var list = await db.Transportations
                    .AsNoTracking()
                    .Where(t => t.TripId == tripId)
                    .Select(t => new
                    {
                        transportationId = t.TransportationId,
                        title = t.Title,
                        description = t.Description,
                        createdByUserId = t.CreatedByUserId,
                        createdAt = t.CreatedAt,
                        isChosen = t.IsChosen,
                        routes = t.Routes.Select(r => new { r.Id, r.Url, r.ContentType, r.FileName, r.UploadedAt }).ToArray(),
                        documents = t.Documents.Select(d => new { d.Id, d.Url, d.ContentType, d.FileName, d.UploadedAt }).ToArray()
                    })
                    .ToListAsync(ct);

                if (list.Count == 0)
                {
                    var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, ct);
                    if (!tripExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                }

                return Results.Ok(list);
            })
            .WithTags("Transportations")
            .WithSummary("List transportations to trip start")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Get detail
        v1.MapGet("/trips/{tripId:guid}/transportations/{transportationId:guid}", async (Guid tripId, Guid transportationId, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations
                    .AsNoTracking()
                    .Include(x => x.Routes)
                    .Include(x => x.Documents)
                    .FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));

                return Results.Ok(new
                {
                    transportationId = t.TransportationId,
                    title = t.Title,
                    description = t.Description,
                    createdByUserId = t.CreatedByUserId,
                    createdAt = t.CreatedAt,
                    isChosen = t.IsChosen,
                    routes = t.Routes.Select(r => new { r.Id, r.Url, r.ContentType, r.FileName, r.UploadedAt }).ToArray(),
                    documents = t.Documents.Select(d => new { d.Id, d.Url, d.ContentType, d.FileName, d.UploadedAt }).ToArray()
                });
            })
            .WithTags("Transportations")
            .WithSummary("Get transportation detail")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Update
        v1.MapPut("/trips/{tripId:guid}/transportations/{transportationId:guid}", async (Guid tripId, Guid transportationId, UpdateTransportationRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));
                t.Title = req.Title;
                t.Description = req.Description;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Transportations")
            .WithSummary("Update transportation")
            .Accepts<UpdateTransportationRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Choose transportation (organizer only)
        v1.MapPatch("/trips/{tripId:guid}/transportations/{transportationId:guid}/choose", async (Guid tripId, Guid transportationId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, ct);
                if (trip is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                if (trip.OrganizerId != me) return Results.Forbid();

                var target = await db.Transportations.FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (target is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));

                var all = await db.Transportations.Where(x => x.TripId == tripId).ToListAsync(ct);
                foreach (var t in all)
                    t.IsChosen = t.TransportationId == transportationId;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Transportations")
            .WithSummary("Choose transportation (exclusive)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        // Delete
        v1.MapDelete("/trips/{tripId:guid}/transportations/{transportationId:guid}", async (Guid tripId, Guid transportationId, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));
                db.Transportations.Remove(t);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Transportations")
            .WithSummary("Delete transportation")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Upload route files (gpx/json). Allow max 3 route files per transportation.
        v1.MapPost("/trips/{tripId:guid}/transportations/{transportationId:guid}/routes", async (Guid tripId, Guid transportationId, HttpRequest request, IWebHostEnvironment env, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.Include(x => x.Routes).FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));

                if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data with files.");
                var form = await request.ReadFormAsync(ct);
                var files = form.Files;
                if (files is null || files.Count == 0) return Results.BadRequest("No files uploaded.");

                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/gpx+xml", "application/json", "application/octet-stream", "text/xml" };
                var existing = t.Routes.Count;
                if (existing >= 3) return Results.BadRequest("This transportation already has the maximum of 3 route files.");
                if (existing + files.Count > 3) return Results.BadRequest($"Too many route files. You can upload at most {3 - existing} more.");

                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var relativeDir = Path.Combine("uploads", "transportations", transportationId.ToString("D"), "routes");
                var absDir = Path.Combine(root, relativeDir);
                Directory.CreateDirectory(absDir);

                var saved = new List<object>();
                foreach (var file in files)
                {
                    if (file.Length == 0) continue;
                    if (!allowed.Contains(file.ContentType)) return Results.BadRequest("Unsupported file type. Only GPX (application/gpx+xml) or JSON (application/json) files are allowed.");

                    var ext = Path.GetExtension(file.FileName);
                    if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10)
                    {
                        ext = file.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ? ".json" : ".gpx";
                    }
                    var name = $"{Guid.NewGuid():N}{ext}";
                    var absPath = Path.Combine(absDir, name);
                    await using (var stream = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        await file.CopyToAsync(stream, ct);
                    }
                    var urlPath = "/" + Path.Combine(relativeDir, name).Replace('\\', '/');
                    var rec = new TransportationRouteRecord
                    {
                        TransportationId = transportationId,
                        Url = urlPath,
                        ContentType = file.ContentType,
                        FileName = file.FileName,
                        UploadedAt = DateTimeOffset.UtcNow
                    };
                    db.TransportationRoutes.Add(rec);
                    saved.Add(new { rec.Id, rec.Url, rec.ContentType, rec.FileName, rec.UploadedAt });
                }

                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/v1/trips/{tripId}/transportations/{transportationId}/routes", new { routes = saved });
            })
            .WithTags("Transportations")
            .WithSummary("Upload route files (GPX/JSON)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        // List routes
        v1.MapGet("/trips/{tripId:guid}/transportations/{transportationId:guid}/routes", async (Guid tripId, Guid transportationId, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.Include(x => x.Routes).AsNoTracking().FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));
                var items = t.Routes.Select(r => new { r.Id, r.Url, r.ContentType, r.FileName, r.UploadedAt }).ToArray();
                return Results.Ok(items);
            })
            .WithTags("Transportations")
            .WithSummary("List route files")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Delete route
        v1.MapDelete("/trips/{tripId:guid}/transportations/{transportationId:guid}/routes/{routeId:int}", async (Guid tripId, Guid transportationId, int routeId, IWebHostEnvironment env, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));
                var item = await db.TransportationRoutes.FirstOrDefaultAsync(r => r.Id == routeId && r.TransportationId == transportationId, ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Route not found"));

                // Delete file
                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var absPath = Path.Combine(root, item.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absPath))
                {
                    try { File.Delete(absPath); } catch { /* ignore */ }
                }

                db.TransportationRoutes.Remove(item);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Transportations")
            .WithSummary("Delete route file")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Upload documents (images/pdf), max 20 per transportation
        v1.MapPost("/trips/{tripId:guid}/transportations/{transportationId:guid}/documents", async (Guid tripId, Guid transportationId, HttpRequest request, IWebHostEnvironment env, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.Include(x => x.Documents).FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));

                if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data with files.");
                var form = await request.ReadFormAsync(ct);
                var files = form.Files;
                if (files is null || files.Count == 0) return Results.BadRequest("No files uploaded.");

                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "application/pdf" };
                var existing = t.Documents.Count;
                if (existing >= 20) return Results.BadRequest("This transportation already has the maximum of 20 documents.");
                if (existing + files.Count > 20) return Results.BadRequest($"Too many documents. You can upload at most {20 - existing} more.");

                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var relativeDir = Path.Combine("uploads", "transportations", transportationId.ToString("D"), "docs");
                var absDir = Path.Combine(root, relativeDir);
                Directory.CreateDirectory(absDir);

                var saved = new List<object>();
                foreach (var file in files)
                {
                    if (file.Length == 0) continue;
                    if (!allowed.Contains(file.ContentType)) return Results.BadRequest("Unsupported file type. Only image/jpeg, image/png, or application/pdf are allowed.");

                    var ext = Path.GetExtension(file.FileName);
                    if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10)
                    {
                        if (file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) ext = ".pdf";
                        else if (file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)) ext = ".png";
                        else ext = ".jpg";
                    }
                    var name = $"{Guid.NewGuid():N}{ext}";
                    var absPath = Path.Combine(absDir, name);
                    await using (var stream = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        await file.CopyToAsync(stream, ct);
                    }
                    var urlPath = "/" + Path.Combine(relativeDir, name).Replace('\\', '/');
                    var rec = new TransportationDocumentRecord
                    {
                        TransportationId = transportationId,
                        Url = urlPath,
                        ContentType = file.ContentType,
                        FileName = file.FileName,
                        UploadedAt = DateTimeOffset.UtcNow
                    };
                    db.TransportationDocuments.Add(rec);
                    saved.Add(new { rec.Id, rec.Url, rec.ContentType, rec.FileName, rec.UploadedAt });
                }

                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/v1/trips/{tripId}/transportations/{transportationId}/documents", new { documents = saved });
            })
            .WithTags("Transportations")
            .WithSummary("Upload transportation documents (images/pdf)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        // List documents
        v1.MapGet("/trips/{tripId:guid}/transportations/{transportationId:guid}/documents", async (Guid tripId, Guid transportationId, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.Include(x => x.Documents).AsNoTracking().FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));
                var items = t.Documents.Select(d => new { d.Id, d.Url, d.ContentType, d.FileName, d.UploadedAt }).ToArray();
                return Results.Ok(items);
            })
            .WithTags("Transportations")
            .WithSummary("List transportation documents")
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Delete document
        v1.MapDelete("/trips/{tripId:guid}/transportations/{transportationId:guid}/documents/{documentId:int}", async (Guid tripId, Guid transportationId, int documentId, IWebHostEnvironment env, AppDbContext db, CancellationToken ct) =>
            {
                var t = await db.Transportations.FirstOrDefaultAsync(x => x.TripId == tripId && x.TransportationId == transportationId, ct);
                if (t is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or transportation not found"));
                var item = await db.TransportationDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.TransportationId == transportationId, ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Document not found"));

                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var absPath = Path.Combine(root, item.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absPath))
                {
                    try { File.Delete(absPath); } catch { /* ignore */ }
                }

                db.TransportationDocuments.Remove(item);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Transportations")
            .WithSummary("Delete transportation document")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return v1;
    }
}
