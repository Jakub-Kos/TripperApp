namespace TripPlanner.Api.Auth;

/// <summary>
/// Configuration options for JWT token issuance and validation.
/// Typically bound from configuration under "Jwt".
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Token issuer (iss).</summary>
    public string Issuer { get; set; } = default!;
    /// <summary>Token audience (aud).</summary>
    public string Audience { get; set; } = default!;
    /// <summary>Symmetric signing key used to sign access tokens.</summary>
    public string Key { get; set; } = default!;
    /// <summary>Access token lifetime in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 15;
    /// <summary>Refresh token lifetime in days.</summary>
    public int RefreshTokenDays { get; set; } = 30;
}