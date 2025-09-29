using System.Net.Http.Json;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.Common;

namespace TripPlanner.Client;

/// <summary>
/// Thin wrapper around authentication endpoints.
/// Uses a bare HttpClient (no auth handler) configured in DI.
/// </summary>
public sealed class AuthClient(HttpClient http)
{
    /// <summary>
    /// Registers a new user account.
    /// Returns true on success, false otherwise.
    /// </summary>
    public async Task<bool> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("/auth/register", req, ct);
        return res.IsSuccessStatusCode;
    }

    /// <summary>
    /// Performs a password login to obtain access and refresh tokens.
    /// Returns null if credentials are invalid.
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("/auth/login", new LoginRequest(email, password), ct);
        if (!res.IsSuccessStatusCode) return null;
        var dto = await res.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        return dto is null ? null : new LoginResponse(dto.AccessToken, dto.RefreshToken, dto.ExpiresInSeconds);
    }

    /// <summary>
    /// Exchanges a refresh token for a new access/refresh pair.
    /// Returns null if the refresh token is invalid/expired.
    /// </summary>
    public async Task<RefreshResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("/auth/refresh", new RefreshRequest(refreshToken), ct);
        if (!res.IsSuccessStatusCode) return null;
        var dto = await res.Content.ReadFromJsonAsync<RefreshResponse>(cancellationToken: ct);
        return dto is null ? null : new RefreshResponse(dto.AccessToken, dto.RefreshToken, dto.ExpiresInSeconds);
    }

    /// <summary>
    /// Logs out the refresh token server-side.
    /// </summary>
    public Task LogoutAsync(string refreshToken, CancellationToken ct = default) =>
        http.PostAsJsonAsync("/auth/logout", new RefreshRequest(refreshToken), ct);
}