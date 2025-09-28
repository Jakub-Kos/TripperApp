using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Gear;

public sealed class GearAssignmentRecord
{
    public Guid AssignmentId { get; set; }
    public Guid GearId { get; set; }
    public Guid ParticipantId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public GearItemRecord? Gear { get; set; }
    public ParticipantRecord? Participant { get; set; }
}
