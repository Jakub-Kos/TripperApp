using System.Security.Cryptography;
using System.Text;

namespace TripPlanner.Api.Auth;

internal static class TokenHasher
{
    // If "pepper" (secret) in config is set, then HMAC will be used; otherwise plain SHA-256.
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