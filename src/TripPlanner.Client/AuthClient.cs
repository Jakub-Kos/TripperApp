using System.Net.Http.Json;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.Common;

namespace TripPlanner.Client;

public sealed class AuthClient(HttpClient http)
{
    public async Task<(string access, string refresh, int expiresIn)?> Login(string email, string password, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("/auth/login", new LoginRequest(email, password), ct);
        if (!res.IsSuccessStatusCode) return null;
        var dto = await res.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        return dto is null ? null : (dto.AccessToken, dto.RefreshToken, dto.ExpiresInSeconds);
    }

    public async Task<(string access, string refresh, int expiresIn)?> Refresh(string refreshToken, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("/auth/refresh", new RefreshRequest(refreshToken), ct);
        if (!res.IsSuccessStatusCode) return null;
        var dto = await res.Content.ReadFromJsonAsync<RefreshResponse>(cancellationToken: ct);
        return dto is null ? null : (dto.AccessToken, dto.RefreshToken, dto.ExpiresInSeconds);
    }

    public Task Logout(string refreshToken, CancellationToken ct = default) =>
        http.PostAsJsonAsync("/auth/logout", new RefreshRequest(refreshToken), ct);
}