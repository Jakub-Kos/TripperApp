namespace TripPlanner.Client;

public interface IAuthState
{
    string? AccessToken { get; }
    DateTimeOffset? AccessTokenExpiresAt { get; }
    string? RefreshToken { get; }

    void SetTokens(string accessToken, int expiresInSeconds, string refreshToken);
    void Clear();
}