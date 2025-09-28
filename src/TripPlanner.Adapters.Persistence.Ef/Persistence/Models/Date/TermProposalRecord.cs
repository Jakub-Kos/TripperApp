using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;

public sealed class TermProposalRecord
{
    public Guid TermProposalId { get; set; }
    public Guid TripId { get; set; }

    // Store as ISO yyyy-MM-dd for simplicity and to match existing DateOptionRecord approach
    public string StartIso { get; set; } = null!;
    public string EndIso { get; set; } = null!;

    // Who created the proposal and when
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public TripRecord Trip { get; set; } = null!;
    public List<TermProposalVoteRecord> Votes { get; set; } = new();
}