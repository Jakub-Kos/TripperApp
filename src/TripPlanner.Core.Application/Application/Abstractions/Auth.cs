namespace TripPlanner.Core.Application.Application.Abstractions;

public sealed record UserInfo(Guid UserId, string Email, string DisplayName, string PasswordHash);
public sealed record RefreshTokenInfo(string Token, Guid UserId, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, UserInfo? User);

public interface IUserRepository
{
    Task<UserInfo?> FindByEmail(string email, CancellationToken ct);
    Task<UserInfo?> FindById(Guid userId, CancellationToken ct);
    Task<Guid> Add(string email, string displayName, string passwordHash, CancellationToken ct);
    Task AddRefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct);
    Task<RefreshTokenInfo?> FindRefreshToken(string tokenHash, CancellationToken ct);
    Task RevokeRefreshToken(string tokenHash, DateTimeOffset revokedAt, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}