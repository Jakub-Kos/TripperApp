namespace TripPlanner.Client;

/// <summary>
/// Holds the current authentication tokens for the client.
/// Implementations should be thread-safe if used across requests.
/// </summary>
public interface IAuthState
{
    /// <summary>Current JWT access token or null if not authenticated.</summary>
    string? AccessToken { get; }

    /// <summary>Expiry timestamp of the access token (UTC), if known.</summary>
    DateTimeOffset? AccessTokenExpiresAt { get; }

    /// <summary>Current refresh token or null.</summary>
    string? RefreshToken { get; }

    /// <summary>Stores new tokens received from login/refresh.</summary>
    void SetTokens(string accessToken, int expiresInSeconds, string refreshToken);

    /// <summary>Clears all stored tokens.</summary>
    void Clear();
}