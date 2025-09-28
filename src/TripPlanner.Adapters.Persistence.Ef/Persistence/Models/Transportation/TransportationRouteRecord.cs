namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Transportation;

public sealed class TransportationRouteRecord
{
    public int Id { get; set; }
    public Guid TransportationId { get; set; }
    public string Url { get; set; } = default!; // relative URL under wwwroot
    public string ContentType { get; set; } = default!; // application/gpx+xml or application/json
    public string FileName { get; set; } = default!;
    public DateTimeOffset UploadedAt { get; set; }
}
