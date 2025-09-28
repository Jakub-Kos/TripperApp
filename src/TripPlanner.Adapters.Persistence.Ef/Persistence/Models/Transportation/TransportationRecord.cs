using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Transportation;

public sealed class TransportationRecord
{
    public Guid TransportationId { get; set; }
    public Guid TripId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<TransportationRouteRecord> Routes { get; set; } = new List<TransportationRouteRecord>();
    public ICollection<TransportationDocumentRecord> Documents { get; set; } = new List<TransportationDocumentRecord>();

    public TripRecord Trip { get; set; } = default!;
}
