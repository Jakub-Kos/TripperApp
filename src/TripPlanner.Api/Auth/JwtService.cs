using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TripPlanner.Api.Auth;

/// <summary>
/// Service for issuing JWT access tokens and random refresh tokens.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Issues a signed JWT access token for the specified user.
    /// </summary>
    /// <param name="userId">Unique identifier of the user (emitted as 'sub').</param>
    /// <param name="email">User email (emitted as 'email').</param>
    /// <param name="extraClaims">Optional extra claims to include.</param>
    /// <returns>The token string and its expiration time (UTC).</returns>
    (string token, DateTimeOffset expiresAt) IssueAccessToken(Guid userId, string email, IEnumerable<Claim>? extraClaims = null);

    /// <summary>
    /// Issues a cryptographically strong random refresh token string.
    /// </summary>
    string IssueRefreshToken();
}

/// <summary>
/// Default implementation of <see cref="IJwtService"/> backed by symmetric signing key from <see cref="JwtOptions"/>.
/// </summary>
public sealed class JwtService(JwtOptions opts) : IJwtService
{
    // Reuse handler instance for performance; it is thread-safe for writing tokens
    private readonly JwtSecurityTokenHandler _handler = new();

    /// <inheritdoc />
    public (string token, DateTimeOffset expiresAt) IssueAccessToken(Guid userId, string email, IEnumerable<Claim>? extraClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(opts.AccessTokenMinutes);

        // Base claims used by the API to identify user and token instance
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (extraClaims is not null) claims.AddRange(extraClaims);

        var jwt = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (_handler.WriteToken(jwt), expires);
    }

    /// <inheritdoc />
    public string IssueRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}