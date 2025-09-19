using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TripPlanner.Wpf.Auth;

public sealed class TokenStore
{
    private readonly string _path;
    public TokenStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TripperApp");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "tokens.dat");
    }

    private sealed class Payload { public string RefreshToken { get; set; } = ""; }

    public void SaveRefreshToken(string refreshToken)
    {
        var json = JsonSerializer.Serialize(new Payload { RefreshToken = refreshToken });
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public string? LoadRefreshToken()
    {
        if (!File.Exists(_path)) return null;
        var protectedBytes = File.ReadAllBytes(_path);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        var payload = JsonSerializer.Deserialize<Payload>(bytes);
        return payload?.RefreshToken;
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}