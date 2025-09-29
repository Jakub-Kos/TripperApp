using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Itinerary;
using TripPlanner.Api.Infrastructure;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Itinerary;

namespace TripPlanner.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for managing itinerary days, items, and routes within a trip.
/// </summary>
public static class ItineraryEndpoints
{
    /// <summary>
    /// Registers itinerary-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapItineraryEndpoints(this IEndpointRouteBuilder v1)
    {
        // Days
        v1.MapGet("/trips/{tripId:guid}/days", async (Guid tripId, AppDbContext db, CancellationToken ct) =>
            {
                var days = await db.Days
                    .AsNoTracking()
                    .Where(d => d.TripId == tripId)
                    .Include(d => d.Items)
                    .Include(d => d.Routes)
                    .OrderBy(d => d.Date)
                    .ToListAsync(ct);

                if (days.Count == 0)
                {
                    var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, ct);
                    if (!tripExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));
                }

                var result = days.Select(MapDay).ToList();
                return Results.Ok(result);
            })
            .WithTags("Itinerary")
            .WithSummary("List days of a trip")
            .Produces<IReadOnlyList<DayDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/{tripId:guid}/days", async (Guid tripId, CreateDayRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, ct);
                if (!tripExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip not found"));

                var rec = new DayRecord
                {
                    DayId = Guid.NewGuid(),
                    TripId = tripId,
                    Date = req.Date,
                    Title = req.Title,
                    Description = req.Description
                };
                db.Days.Add(rec);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/v1/trips/{tripId}/days/{rec.DayId:D}", MapDay(rec));
            })
            .WithTags("Itinerary")
            .WithSummary("Create a day")
            .Accepts<CreateDayRequest>("application/json")
            .Produces<DayDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        v1.MapGet("/trips/{tripId:guid}/days/{dayId:guid}", async (Guid tripId, Guid dayId, AppDbContext db, CancellationToken ct) =>
            {
                var day = await db.Days.AsNoTracking()
                    .Include(d => d.Items)
                    .Include(d => d.Routes)
                    .FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                return Results.Ok(MapDay(day));
            })
            .WithTags("Itinerary")
            .WithSummary("Get day detail")
            .Produces<DayDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPut("/trips/{tripId:guid}/days/{dayId:guid}", async (Guid tripId, Guid dayId, UpdateDayRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var day = await db.Days.FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                if (req.Date is not null) day.Date = req.Date;
                if (req.Title is not null) day.Title = req.Title;
                if (req.Description is not null) day.Description = req.Description;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Update a day")
            .Accepts<UpdateDayRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapDelete("/trips/{tripId:guid}/days/{dayId:guid}", async (Guid tripId, Guid dayId, AppDbContext db, CancellationToken ct) =>
            {
                var day = await db.Days.FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                db.Days.Remove(day);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Delete a day")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPatch("/trips/{tripId:guid}/days/{dayId:guid}/anchors", async (Guid tripId, Guid dayId, UpdateDayAnchorsRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var day = await db.Days.FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));

                if (req.StartLocation is not null)
                {
                    day.StartLocation = new LocationEmbeddable
                    {
                        Name = req.StartLocation.Name,
                        Lat = req.StartLocation.Lat,
                        Lon = req.StartLocation.Lon,
                        Address = req.StartLocation.Address,
                        PlaceId = req.StartLocation.PlaceId
                    };
                }
                if (req.EndLocation is not null)
                {
                    day.EndLocation = new LocationEmbeddable
                    {
                        Name = req.EndLocation.Name,
                        Lat = req.EndLocation.Lat,
                        Lon = req.EndLocation.Lon,
                        Address = req.EndLocation.Address,
                        PlaceId = req.EndLocation.PlaceId
                    };
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Update day anchors (start/end locations)")
            .Accepts<UpdateDayAnchorsRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // Day Items
        v1.MapGet("/trips/{tripId:guid}/days/{dayId:guid}/items", async (Guid tripId, Guid dayId, AppDbContext db, CancellationToken ct) =>
            {
                var exists = await db.Days.AnyAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (!exists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                var items = await db.DayItems.AsNoTracking()
                    .Where(i => i.DayId == dayId)
                    .OrderBy(i => i.OrderIndex)
                    .ToListAsync(ct);
                var dtos = items.Select(MapItem).ToList();
                return Results.Ok(dtos);
            })
            .WithTags("Itinerary")
            .WithSummary("List items for a day")
            .Produces<IReadOnlyList<DayItemDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/{tripId:guid}/days/{dayId:guid}/items", async (Guid tripId, Guid dayId, CreateDayItemRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var day = await db.Days.Include(d => d.Items).FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                var nextIndex = day.Items.Count == 0 ? 0 : day.Items.Max(i => i.OrderIndex) + 1;
                var item = new DayItemRecord
                {
                    ItemId = Guid.NewGuid(),
                    DayId = dayId,
                    Type = req.Type,
                    Name = req.Name,
                    Lat = req.Lat,
                    Lon = req.Lon,
                    ScheduledStart = req.ScheduledStart,
                    DurationMinutes = req.DurationMinutes,
                    Notes = req.Notes,
                    Link = req.Link,
                    OrderIndex = nextIndex
                };
                db.DayItems.Add(item);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/v1/trips/{tripId}/items/{item.ItemId:D}", MapItem(item));
            })
            .WithTags("Itinerary")
            .WithSummary("Create day item")
            .Accepts<CreateDayItemRequest>("application/json")
            .Produces<DayItemDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapGet("/trips/{tripId:guid}/items/{itemId:guid}", async (Guid tripId, Guid itemId, AppDbContext db, CancellationToken ct) =>
            {
                var item = await db.DayItems
                    .Join(db.Days, i => i.DayId, d => d.DayId, (i, d) => new { i, d })
                    .Where(x => x.d.TripId == tripId && x.i.ItemId == itemId)
                    .Select(x => x.i)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or item not found"));
                return Results.Ok(MapItem(item));
            })
            .WithTags("Itinerary")
            .WithSummary("Get item detail")
            .Produces<DayItemDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPut("/trips/{tripId:guid}/items/{itemId:guid}", async (Guid tripId, Guid itemId, UpdateDayItemRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var item = await db.DayItems
                    .Join(db.Days, i => i.DayId, d => d.DayId, (i, d) => new { i, d })
                    .Where(x => x.d.TripId == tripId && x.i.ItemId == itemId)
                    .Select(x => x.i)
                    .FirstOrDefaultAsync(ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or item not found"));

                if (req.Type is not null) item.Type = req.Type;
                if (req.Name is not null) item.Name = req.Name;
                item.Lat = req.Lat;
                item.Lon = req.Lon;
                item.ScheduledStart = req.ScheduledStart;
                item.DurationMinutes = req.DurationMinutes;
                item.Notes = req.Notes;
                item.Link = req.Link;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Update day item")
            .Accepts<UpdateDayItemRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapDelete("/trips/{tripId:guid}/items/{itemId:guid}", async (Guid tripId, Guid itemId, AppDbContext db, CancellationToken ct) =>
            {
                var item = await db.DayItems
                    .Join(db.Days, i => i.DayId, d => d.DayId, (i, d) => new { i, d })
                    .Where(x => x.d.TripId == tripId && x.i.ItemId == itemId)
                    .Select(x => x.i)
                    .FirstOrDefaultAsync(ct);
                if (item is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or item not found"));
                db.DayItems.Remove(item);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Delete day item")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPatch("/trips/{tripId:guid}/days/{dayId:guid}/items/reorder", async (Guid tripId, Guid dayId, ReorderDayItemsRequest req, AppDbContext db, CancellationToken ct) =>
            {
                var day = await db.Days.Include(d => d.Items).FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                var idToItem = day.Items.ToDictionary(i => i.ItemId, i => i);

                // Validate all ids first
                var parsedIds = new List<Guid>(req.OrderedItemIds.Count);
                foreach (var idStr in req.OrderedItemIds)
                {
                    if (!Guid.TryParse(idStr, out var gid)) return Results.BadRequest("Invalid item id format.");
                    if (!idToItem.ContainsKey(gid)) return Results.BadRequest("Item id does not belong to the day.");
                    parsedIds.Add(gid);
                }

                // Step 1: move to a safe temporary range to avoid unique constraint conflicts on (DayId, OrderIndex)
                // Use large offset based on current max
                var currentMax = day.Items.Count == 0 ? 0 : day.Items.Max(i => i.OrderIndex);
                var tempBase = currentMax + 1000;
                for (var i = 0; i < parsedIds.Count; i++)
                {
                    var it = idToItem[parsedIds[i]];
                    it.OrderIndex = tempBase + i;
                }
                await db.SaveChangesAsync(ct);

                // Step 2: set the final desired order (0..n-1)
                for (var i = 0; i < parsedIds.Count; i++)
                {
                    var it = idToItem[parsedIds[i]];
                    it.OrderIndex = i;
                }
                await db.SaveChangesAsync(ct);

                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Reorder items of a day")
            .Accepts<ReorderDayItemsRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        // Day Routes
        v1.MapGet("/trips/{tripId:guid}/days/{dayId:guid}/routes", async (Guid tripId, Guid dayId, AppDbContext db, CancellationToken ct) =>
            {
                var dayExists = await db.Days.AsNoTracking().AnyAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (!dayExists) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));
                var routes = await db.DayRouteFiles.AsNoTracking()
                    .Where(r => r.DayId == dayId)
                    .OrderBy(r => r.RouteId)
                    .Select(r => new RouteFileDto(r.RouteId, r.FileName, r.MediaType, r.SizeBytes, r.UploadedAt.ToUniversalTime().ToString("O"), r.UploadedByParticipantId.ToString("D")))
                    .ToListAsync(ct);
                return Results.Ok(routes);
            })
            .WithTags("Itinerary")
            .WithSummary("List day route files")
            .Produces<IReadOnlyList<RouteFileDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapPost("/trips/{tripId:guid}/days/{dayId:guid}/routes", async (Guid tripId, Guid dayId, HttpRequest request, IWebHostEnvironment env, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
            {
                var sub = user.FindFirst("sub")?.Value ?? user.FindFirst("nameid")?.Value;
                if (!Guid.TryParse(sub, out var me)) return Results.Unauthorized();

                var day = await db.Days.FirstOrDefaultAsync(d => d.TripId == tripId && d.DayId == dayId, ct);
                if (day is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Trip or day not found"));

                if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data with a file.");
                var form = await request.ReadFormAsync(ct);
                var file = form.Files.FirstOrDefault();
                if (file is null) return Results.BadRequest("No file uploaded.");

                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/gpx+xml", "application/geo+json", "application/json" };
                if (!allowed.Contains(file.ContentType)) return Results.BadRequest("Unsupported file type. Only GPX or GeoJSON/JSON are allowed.");

                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var relativeDir = Path.Combine("uploads", "days", dayId.ToString("D"), "routes");
                var absDir = Path.Combine(root, relativeDir);
                Directory.CreateDirectory(absDir);

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10)
                {
                    if (file.ContentType.Equals("application/gpx+xml", StringComparison.OrdinalIgnoreCase)) ext = ".gpx";
                    else ext = ".json";
                }
                var name = $"{Guid.NewGuid():N}{ext}";
                var absPath = Path.Combine(absDir, name);
                await using (var stream = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write))
                {
                    await file.CopyToAsync(stream, ct);
                }
                var urlPath = "/" + Path.Combine(relativeDir, name).Replace('\\', '/');

                var route = new DayRouteFileRecord
                {
                    DayId = dayId,
                    Url = urlPath,
                    FileName = file.FileName,
                    MediaType = file.ContentType,
                    SizeBytes = file.Length,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByParticipantId = me
                };
                db.DayRouteFiles.Add(route);
                await db.SaveChangesAsync(ct);

                var dto = new RouteFileDto(route.RouteId, route.FileName, route.MediaType, route.SizeBytes, route.UploadedAt.ToUniversalTime().ToString("O"), route.UploadedByParticipantId.ToString("D"));
                return Results.Created($"/api/v1/trips/{tripId}/days/{dayId}/routes/{route.RouteId}", dto);
            })
            .WithTags("Itinerary")
            .WithSummary("Upload a route file (GPX/GeoJSON/JSON)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<RouteFileDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        v1.MapGet("/trips/{tripId:guid}/days/{dayId:guid}/routes/{routeId}", async (Guid tripId, Guid dayId, string routeId, AppDbContext db, IWebHostEnvironment env, CancellationToken ct) =>
            {
                if (!int.TryParse(routeId, out var rid))
                {
                    return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Route not found"));
                }

                // Strict match by trip/day/id
                var route = await db.DayRouteFiles
                    .Join(db.Days, r => r.DayId, d => d.DayId, (r, d) => new { r, d })
                    .Where(x => x.d.TripId == tripId && x.d.DayId == dayId && x.r.RouteId == rid)
                    .Select(x => x.r)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);

                if (route is null)
                    return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Route not found"));

                // Verify the target file actually exists before redirecting
                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var absPath = Path.Combine(root, route.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absPath))
                    return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Route file not found"));

                if (env.IsDevelopment()) System.Console.WriteLine($"[DEBUG_LOG] Redirecting to URL: {route.Url}");
                return Results.Redirect(route.Url);
            })
            .WithTags("Itinerary")
            .WithSummary("Download route file (redirect to static URL)")
            .Produces(StatusCodes.Status302Found)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        v1.MapDelete("/trips/{tripId:guid}/days/{dayId:guid}/routes/{routeId:int}", async (Guid tripId, Guid dayId, int routeId, IWebHostEnvironment env, AppDbContext db, CancellationToken ct) =>
            {
                var route = await db.DayRouteFiles
                    .Join(db.Days, r => r.DayId, d => d.DayId, (r, d) => new { r, d })
                    .Where(x => x.d.TripId == tripId && x.d.DayId == dayId && x.r.RouteId == routeId)
                    .Select(x => x.r)
                    .FirstOrDefaultAsync(ct);
                if (route is null) return Results.NotFound(new ErrorResponse(ErrorCodes.NotFound, "Route not found"));

                var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var absPath = Path.Combine(root, route.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absPath))
                {
                    try { File.Delete(absPath); } catch { /* ignore */ }
                }

                db.DayRouteFiles.Remove(route);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags("Itinerary")
            .WithSummary("Delete route file")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return v1;
    }

    private static DayDto MapDay(DayRecord d)
    {
        var start = d.StartLocation is null ? null : new LocationDto(d.StartLocation.Name, d.StartLocation.Lat, d.StartLocation.Lon, d.StartLocation.Address, d.StartLocation.PlaceId);
        var end = d.EndLocation is null ? null : new LocationDto(d.EndLocation.Name, d.EndLocation.Lat, d.EndLocation.Lon, d.EndLocation.Address, d.EndLocation.PlaceId);
        var items = (d.Items ?? Array.Empty<DayItemRecord>()).OrderBy(i => i.OrderIndex).Select(MapItem).ToList();
        var routes = (d.Routes ?? Array.Empty<DayRouteFileRecord>()).OrderBy(r => r.RouteId).Select(r => new RouteFileDto(r.RouteId, r.FileName, r.MediaType, r.SizeBytes, r.UploadedAt.ToUniversalTime().ToString("O"), r.UploadedByParticipantId.ToString("D")) ).ToList();
        return new DayDto(d.DayId.ToString("D"), d.TripId.ToString("D"), d.Date, d.Title, d.Description, start, end, items, routes);
    }

    private static DayItemDto MapItem(DayItemRecord i)
    {
        return new DayItemDto(i.ItemId.ToString("D"), i.DayId.ToString("D"), i.Type, i.Name, i.Lat, i.Lon, i.ScheduledStart, i.DurationMinutes, i.Notes, i.Link, i.OrderIndex);
    }
}
