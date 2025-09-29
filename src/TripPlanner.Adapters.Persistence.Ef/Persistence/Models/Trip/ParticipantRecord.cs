using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

public class ParticipantRecord
{
    public long Id { get; set; }
    public Guid TripId { get; set; }
    public Guid? UserId { get; set; }
    public Guid ParticipantId { get; set; }
    public bool IsPlaceholder { get; set; }
    public string DisplayName { get; set; } = "";
    
    public DateTimeOffset? ClaimedAt { get; set; }   
    public Guid CreatedByUserId { get; set; }      
    public TripRecord Trip { get; set; } = default!;
    public UserRecord? User { get; set; }
}
