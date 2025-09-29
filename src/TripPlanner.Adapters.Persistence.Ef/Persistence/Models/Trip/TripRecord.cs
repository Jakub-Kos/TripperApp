using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Transportation;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

public sealed class TripRecord
{
    public Guid TripId { get; set; }
    public string Name { get; set; } = null!;
    public Guid OrganizerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string DescriptionMarkdown { get; set; } = "";
    public bool IsFinished { get; set; } = false;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public List<ParticipantRecord> Participants { get; set; } = new();
    public List<DateOptionRecord> DateOptions { get; set; } = new();
    public ICollection<DestinationRecord> Destinations { get; set; } = new List<DestinationRecord>();

    public List<TermProposalRecord> TermProposals { get; set; } = new();
    public ICollection<TransportationRecord> Transportations { get; set; } = new List<TransportationRecord>();
}