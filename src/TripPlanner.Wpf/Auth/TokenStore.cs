using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TripPlanner.Wpf.Auth;

/// <summary>
/// Persists a small encrypted payload for the current Windows user containing
/// the refresh token (and an optional nickname). Uses DPAPI via ProtectedData.
/// </summary>
public sealed class TokenStore
{
    private readonly string _path;
    public TokenStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TripperApp");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "tokens.dat");
    }

    private sealed class Payload
    {
        public string RefreshToken { get; set; } = "";
        public string? Nickname { get; set; }
    }

    private Payload LoadPayload()
    {
        if (!File.Exists(_path)) return new Payload();
        var protectedBytes = File.ReadAllBytes(_path);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        try
        {
            return JsonSerializer.Deserialize<Payload>(bytes) ?? new Payload();
        }
        catch
        {
            return new Payload();
        }
    }

    private void SavePayload(Payload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public void SaveRefreshToken(string refreshToken)
    {
        var payload = LoadPayload();
        payload.RefreshToken = refreshToken;
        SavePayload(payload);
    }

    public string? LoadRefreshToken()
    {
        var payload = LoadPayload();
        return payload.RefreshToken;
    }

    public void SaveNickname(string? nickname)
    {
        var payload = LoadPayload();
        payload.Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname;
        SavePayload(payload);
    }

    public string? LoadNickname()
    {
        var payload = LoadPayload();
        return payload.Nickname;
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}