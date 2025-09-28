namespace TripPlanner.Core.Contracts.Contracts.V1.Itinerary;

public sealed record LocationDto(
    string? Name,
    double Lat,
    double Lon,
    string? Address,
    string? PlaceId);

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

public sealed record RouteFileDto(
    int RouteId,
    string FileName,
    string MediaType,
    long SizeBytes,
    string UploadedAt,
    string UploadedByParticipantId);

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
public sealed record CreateDayRequest(string Date, string? Title, string? Description);
public sealed record UpdateDayRequest(string? Date, string? Title, string? Description);
public sealed record UpdateDayAnchorsRequest(LocationDto? StartLocation, LocationDto? EndLocation);

public sealed record CreateDayItemRequest(
    string Type,
    string Name,
    double? Lat,
    double? Lon,
    string? ScheduledStart,
    int? DurationMinutes,
    string? Notes,
    string? Link);

public sealed record UpdateDayItemRequest(
    string? Type,
    string? Name,
    double? Lat,
    double? Lon,
    string? ScheduledStart,
    int? DurationMinutes,
    string? Notes,
    string? Link);

public sealed record ReorderDayItemsRequest(IReadOnlyList<string> OrderedItemIds);
