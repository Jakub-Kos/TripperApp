namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models;

public sealed class UserRecord
{
    public string UserId { get; set; } = Guid.NewGuid().ToString("D");
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    public ICollection<RefreshTokenRecord> RefreshTokens { get; set; } = new List<RefreshTokenRecord>();
}