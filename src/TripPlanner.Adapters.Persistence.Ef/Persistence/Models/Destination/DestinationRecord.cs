using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;

public sealed class DestinationRecord
{
    public Guid DestinationId { get; set; }
    public Guid TripId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public ICollection<DestinationImageRecord> Images { get; set; } = new List<DestinationImageRecord>();
    public ICollection<DestinationVoteRecord> Votes { get; set; } = new List<DestinationVoteRecord>();
    public TripRecord Trip { get; set; } = default!;
}