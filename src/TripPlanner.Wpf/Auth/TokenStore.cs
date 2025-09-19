using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TripPlanner.Wpf.Auth;

public sealed class TokenStore
{
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TripPlanner");

    private string FilePath => Path.Combine(_folder, "rtoken.bin");

    public void SaveRefreshToken(string refreshToken)
    {
        Directory.CreateDirectory(_folder);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, protectedBytes);
    }

    public string? TryLoadRefreshToken()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(FilePath);
            var data = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch { return null; }
    }

    public void Clear() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}