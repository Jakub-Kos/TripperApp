using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;

public sealed class TermProposalRecord
{
    public Guid TermProposalId { get; set; }
    public Guid TripId { get; set; }

    // Store as ISO yyyy-MM-dd for simplicity
    public string StartIso { get; set; } = null!;
    public string EndIso { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsChosen { get; set; }

    public TripRecord Trip { get; set; } = null!;
    public List<TermProposalVoteRecord> Votes { get; set; } = new();
}