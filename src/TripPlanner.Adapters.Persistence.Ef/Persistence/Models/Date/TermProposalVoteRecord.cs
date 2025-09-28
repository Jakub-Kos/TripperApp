namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;

public sealed class TermProposalVoteRecord
{
    public Guid TermProposalId { get; set; }
    public Guid ParticipantId { get; set; }

    public TermProposalRecord TermProposal { get; set; } = null!;
}