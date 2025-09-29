using TripPlanner.Client;

namespace TripPlanner.Wpf.Auth;

/// <summary>
/// Simple in-memory implementation of IAuthState for WPF that also writes the refresh token
/// to disk via TokenStore so the user stays signed in between sessions.
/// </summary>
public sealed class WpfAuthState : IAuthState
{
    private readonly TokenStore _store;

    public WpfAuthState(TokenStore store) => _store = store;

    public string? AccessToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; private set; }
    public string? RefreshToken { get; private set; }

    public void SetTokens(string accessToken, int expiresInSeconds, string refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        _store.SaveRefreshToken(refreshToken);
    }

    public void Clear()
    {
        AccessToken = null;
        AccessTokenExpiresAt = null;
        RefreshToken = null;
        _store.Clear();
    }

    public void LoadRefreshTokenFromDisk() => RefreshToken = _store.LoadRefreshToken();
}