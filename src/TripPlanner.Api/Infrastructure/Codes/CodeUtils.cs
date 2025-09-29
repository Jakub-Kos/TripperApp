using System.Security.Cryptography;
using System.Text;

namespace TripPlanner.Api.Infrastructure;

/// <summary>
/// Helpers for generating and working with short human-friendly codes and hashes.
/// </summary>
public static class CodeUtils
{
    /// <summary>
    /// Normalizes a human-entered code by trimming spaces, uppercasing, and removing separators.
    /// </summary>
    public static string NormalizeCode(string raw)
        => new(raw.Trim().ToUpperInvariant().Where(ch => ch != '-' && ch != ' ').ToArray());

    /// <summary>
    /// Computes a lowercase hex SHA-256 hash of the input text.
    /// </summary>
    public static string Hash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Generates a short, URL-safe code using a Crockford Base32 alphabet (avoids ambiguous characters).
    /// </summary>
    public static string GenerateFriendlyCode(int length)
    {
        // Crockford Base32 (no I, L, O, U)
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var numBytes = (int)Math.Ceiling(length * 5 / 8.0);
        Span<byte> buffer = stackalloc byte[numBytes];
        RandomNumberGenerator.Fill(buffer);

        var bits = 0;
        var value = 0;
        var output = new StringBuilder(length);
        foreach (var b in buffer)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5 && output.Length < length)
            {
                var index = (value >> (bits - 5)) & 31;
                output.Append(alphabet[index]);
                bits -= 5;
            }
        }
        while (output.Length < length)
            output.Append(alphabet[RandomNumberGenerator.GetInt32(32)]);
        return output.ToString();
    }
}