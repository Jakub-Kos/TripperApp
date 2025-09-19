namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models;

public sealed class DestinationVoteRecord
{
    public int Id { get; set; }
    public Guid DestinationId { get; set; }
    public Guid UserId { get; set; }
    public DestinationRecord Destination { get; set; } = default!;
}