namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;

public sealed class RefreshTokenRecord
{
    public int Id { get; set; }
    public Guid UserId { get; set; } = Guid.Empty;
    public string Token { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public UserRecord? User { get; set; }
}