namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;

public sealed class DestinationVoteRecord
{
    public int Id { get; set; }
    public Guid DestinationId { get; set; }
    
    public Guid ParticipantId { get; set; }
    public Guid UserId { get; set; } // TODO delete
    
    public DestinationRecord Destination { get; set; } = default!;
}