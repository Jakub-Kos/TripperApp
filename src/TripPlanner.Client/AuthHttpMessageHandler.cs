using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Client;

public sealed class AuthState
{
    public string? AccessToken { get; set; }
    public DateTimeOffset? AccessExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
}

public sealed class AuthHttpMessageHandler(AuthState state, Func<Task<(string access, string refresh, int expiresIn)?> > refreshFunc)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(state.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", state.AccessToken);

        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(state.RefreshToken))
        {
            response.Dispose();

            var refreshed = await refreshFunc();
            if (refreshed is not null)
            {
                state.AccessToken = refreshed.Value.access;
                state.AccessExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.Value.expiresIn);
                state.RefreshToken = refreshed.Value.refresh;

                var retry = await request.CloneAsync(ct);   // note: async clone
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", state.AccessToken);
                return await base.SendAsync(retry, ct);
            }
        }

        return response;
    }
}

file static class HttpRequestMessageExtensions
{
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage req, CancellationToken ct = default)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version,
#if NET6_0_OR_GREATER
            VersionPolicy = req.VersionPolicy
#endif
        };

        // headers
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        // content
        if (req.Content is not null)
            clone.Content = await req.Content.CloneAsync(ct);

        return clone;
    }

    private static async Task<HttpContent> CloneAsync(this HttpContent content, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);   // <-- async API
        ms.Position = 0;

        var copy = new StreamContent(ms);
        foreach (var h in content.Headers)
            copy.Headers.TryAddWithoutValidation(h.Key, h.Value);

        return copy;
    }
}