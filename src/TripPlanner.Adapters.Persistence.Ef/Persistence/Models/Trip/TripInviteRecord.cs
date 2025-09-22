namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

public sealed class TripInviteRecord
{
    public Guid InviteId { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public string CodeHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxUses { get; set; } = 10;
    public int Uses { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}