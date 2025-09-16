namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models;

public sealed class TripRecord
{
    public Guid TripId { get; set; }
    public string Name { get; set; } = null!;
    public Guid OrganizerId { get; set; }

    public List<TripParticipantRecord> Participants { get; set; } = new();
}

public sealed class TripParticipantRecord
{
    public long Id { get; set; }         // identity PK
    public Guid TripId { get; set; }     // FK
    public Guid UserId { get; set; }
}