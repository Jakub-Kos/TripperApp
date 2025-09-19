namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models;

public sealed class RefreshTokenRecord
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public UserRecord? User { get; set; }
}