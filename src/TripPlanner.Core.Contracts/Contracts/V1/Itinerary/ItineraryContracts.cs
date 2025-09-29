namespace TripPlanner.Core.Contracts.Contracts.V1.Itinerary;

/// <summary>
/// A geographic location optionally enriched with a name and external place identifier.
/// </summary>
/// <param name="Name">Optional label for display purposes.</param>
/// <param name="Lat">Latitude in WGS84.</param>
/// <param name="Lon">Longitude in WGS84.</param>
/// <param name="Address">Optional human-readable address.</param>
/// <param name="PlaceId">Optional provider-specific place identifier.</param>
public sealed record LocationDto(
    string? Name,
    double Lat,
    double Lon,
    string? Address,
    string? PlaceId);

/// <summary>
/// An item scheduled within a day of the itinerary (POI, activity, note, etc.).
/// </summary>
/// <param name="ItemId">Identifier of this item.</param>
/// <param name="DayId">Identifier of the parent day.</param>
/// <param name="Type">Item category: POI, Restaurant, Shop, Activity, Note.</param>
/// <param name="Name">Title or short name.</param>
/// <param name="Lat">Optional latitude.</param>
/// <param name="Lon">Optional longitude.</param>
/// <param name="ScheduledStart">Planned start time in ISO format.</param>
/// <param name="DurationMinutes">Expected duration in minutes.</param>
/// <param name="Notes">Optional free-form notes.</param>
/// <param name="Link">Optional external link for details.</param>
/// <param name="OrderIndex">Position within the day.</param>
public sealed record DayItemDto(
    string ItemId,
    string DayId,
    string Type, // POI, Restaurant, Shop, Activity, Note
    string Name,
    double? Lat,
    double? Lon,
    string? ScheduledStart,
    int? DurationMinutes,
    string? Notes,
    string? Link,
    int OrderIndex);

/// <summary>
/// Metadata about a route file uploaded for navigation (e.g., GPX).
/// </summary>
/// <param name="RouteId">Identifier of the file/route.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="MediaType">MIME type.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="UploadedAt">ISO timestamp of upload.</param>
/// <param name="UploadedByParticipantId">Participant who uploaded the file.</param>
public sealed record RouteFileDto(
    int RouteId,
    string FileName,
    string MediaType,
    long SizeBytes,
    string UploadedAt,
    string UploadedByParticipantId);

/// <summary>
/// A single day in the trip itinerary with items and route files.
/// </summary>
/// <param name="DayId">Identifier of the day.</param>
/// <param name="TripId">Identifier of the trip.</param>
/// <param name="Date">Calendar date (YYYY-MM-DD).</param>
/// <param name="Title">Optional day title.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="StartLocation">Optional start location.</param>
/// <param name="EndLocation">Optional end location.</param>
/// <param name="Items">Ordered items for the day.</param>
/// <param name="Routes">Associated route files.</param>
public sealed record DayDto(
    string DayId,
    string TripId,
    string Date,
    string? Title,
    string? Description,
    LocationDto? StartLocation,
    LocationDto? EndLocation,
    IReadOnlyList<DayItemDto> Items,
    IReadOnlyList<RouteFileDto> Routes);

// Requests
/// <summary>Create a new day.</summary>
public sealed record CreateDayRequest(string Date, string? Title, string? Description);

/// <summary>Update an existing day; nulls mean "no change".</summary>
public sealed record UpdateDayRequest(string? Date, string? Title, string? Description);

/// <summary>Update day start and end anchors.</summary>
public sealed record UpdateDayAnchorsRequest(LocationDto? StartLocation, LocationDto? EndLocation);

/// <summary>Create a new day item.</summary>
public sealed record CreateDayItemRequest(
    string Type,
    string Name,
    double? Lat,
    double? Lon,
    string? ScheduledStart,
    int? DurationMinutes,
    string? Notes,
    string? Link);

/// <summary>Update an existing day item; nulls mean "no change".</summary>
public sealed record UpdateDayItemRequest(
    string? Type,
    string? Name,
    double? Lat,
    double? Lon,
    string? ScheduledStart,
    int? DurationMinutes,
    string? Notes,
    string? Link);

/// <summary>Reorder items within a day by their identifiers.</summary>
public sealed record ReorderDayItemsRequest(IReadOnlyList<string> OrderedItemIds);
