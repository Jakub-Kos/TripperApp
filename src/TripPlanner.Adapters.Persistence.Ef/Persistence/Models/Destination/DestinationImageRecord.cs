namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;

public sealed class DestinationImageRecord
{
    public int Id { get; set; }
    public Guid DestinationId { get; set; }
    public string Url { get; set; } = null!;
    public DestinationRecord Destination { get; set; } = null!;
}