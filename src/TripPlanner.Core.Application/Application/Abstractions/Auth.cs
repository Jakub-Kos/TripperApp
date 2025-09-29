namespace TripPlanner.Core.Application.Application.Abstractions;

/// <summary>
/// Lightweight user info projection used by the application layer for auth flows.
/// </summary>
public sealed record UserInfo(Guid UserId, string Email, string DisplayName, string PasswordHash);

/// <summary>
/// Refresh token information, including optional linkage to the user and revocation state.
/// </summary>
public sealed record RefreshTokenInfo(string Token, Guid UserId, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, UserInfo? User);

/// <summary>
/// Persistence abstraction for users and their refresh tokens.
/// </summary>
public interface IUserRepository
{
    /// <summary>Returns user by email or null if not found.</summary>
    Task<UserInfo?> FindByEmail(string email, CancellationToken ct);

    /// <summary>Returns user by ID or null if not found.</summary>
    Task<UserInfo?> FindById(Guid userId, CancellationToken ct);

    /// <summary>Creates a user and returns its generated ID.</summary>
    Task<Guid> Add(string email, string displayName, string passwordHash, CancellationToken ct);

    /// <summary>Stores a new refresh token for a user.</summary>
    Task AddRefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct);

    /// <summary>Finds a refresh token by its hash (if present).</summary>
    Task<RefreshTokenInfo?> FindRefreshToken(string tokenHash, CancellationToken ct);

    /// <summary>Revokes the refresh token at a specific timestamp.</summary>
    Task RevokeRefreshToken(string tokenHash, DateTimeOffset revokedAt, CancellationToken ct);

    /// <summary>Persists pending changes.</summary>
    Task SaveChanges(CancellationToken ct);
}

/// <summary>
/// Abstraction over system clock to facilitate testing and determinism.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC timestamp.</summary>
    DateTimeOffset UtcNow { get; }
}