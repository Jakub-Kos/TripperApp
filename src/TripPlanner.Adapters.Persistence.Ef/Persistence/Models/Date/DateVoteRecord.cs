namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;

public sealed class DateVoteRecord
{
    public long Id { get; set; }
    public Guid DateOptionId { get; set; }
    
    public Guid ParticipantId { get; set; }
    public Guid? UserId { get; set; } // TODO delete
}