using System.Security.Cryptography;
using System.Text;

namespace TripPlanner.Api.Auth;

/// <summary>
/// Utility for hashing tokens for storage/lookup. Supports optional HMAC with a secret pepper.
/// </summary>
internal static class TokenHasher
{
    /// <summary>
    /// Computes a hex-encoded hash of the provided token. If a pepper is provided, uses HMAC-SHA256; otherwise SHA-256.
    /// </summary>
    public static string Hash(string token, string? pepper = null)
    {
        if (!string.IsNullOrWhiteSpace(pepper))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(token)));
        }

        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
    }
}