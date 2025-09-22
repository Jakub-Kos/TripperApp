namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

public sealed class PlaceholderClaimRecord
{
    public Guid ClaimId { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Guid ParticipantId { get; set; } // placeholder to claim
    public string CodeHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}
