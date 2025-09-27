using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TripPlanner.Api.Auth;

public interface IJwtService
{
    (string token, DateTimeOffset expiresAt) IssueAccessToken(Guid userId, string email, IEnumerable<Claim>? extraClaims = null);
    string IssueRefreshToken();
}

public sealed class JwtService(JwtOptions opts) : IJwtService
{
    private readonly JwtSecurityTokenHandler _handler = new();

    public (string token, DateTimeOffset expiresAt) IssueAccessToken(Guid userId, string email, IEnumerable<Claim>? extraClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(opts.AccessTokenMinutes);

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

    public string IssueRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}