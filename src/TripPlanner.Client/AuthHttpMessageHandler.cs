using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TripPlanner.Client;

/// <summary>
/// Delegating handler that attaches Bearer access token and refreshes on 401 once.
/// Safe retry for idempotent requests only.
/// </summary>
public sealed class AuthHttpMessageHandler : DelegatingHandler
{
    private readonly IAuthState _state;
    private readonly AuthClient _auth;
    private readonly SemaphoreSlim _mutex = new(1,1);

    public AuthHttpMessageHandler(IAuthState state, AuthClient auth)
    {
        _state = state;
        _auth = auth;
    }

    /// <summary>
    /// Attaches access token, handles a single 401 by attempting token refresh and safe retry for GET/HEAD.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await AttachAccessAsync(request, ct);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return response;

        // Try refresh once
        if (string.IsNullOrEmpty(_state.RefreshToken))
            return response;

        await _mutex.WaitAsync(ct);
        try
        {
            // Another request might have refreshed while we waited; re-check
            if (_state.AccessToken is null || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var refreshed = await _auth.RefreshAsync(_state.RefreshToken!, ct);
                if (refreshed is null)
                    throw new InvalidOperationException("Token refresh failed.");
                _state.SetTokens(refreshed.AccessToken, refreshed.ExpiresInSeconds, refreshed.RefreshToken);
            }
        }
        catch
        {
            _state.Clear(); // ensure clean slate
            return response; // surface 401
        }
        finally
        {
            _mutex.Release();
        }

        // retry once with new token ONLY for idempotent requests without content (avoid reusing disposed content streams)
        if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
        {
            var retry = new HttpRequestMessage(request.Method, request.RequestUri);
            // copy headers except Authorization (will be re-added)
            foreach (var h in request.Headers)
            {
                if (string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)) continue;
                retry.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            await AttachAccessAsync(retry, ct);
            return await base.SendAsync(retry, ct);
        }

        // For non-idempotent or content-bearing requests, do not retry to avoid stream reuse issues
        return response;
    }

    private Task AttachAccessAsync(HttpRequestMessage req, CancellationToken _)
    {
        if (!string.IsNullOrWhiteSpace(_state.AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _state.AccessToken);
        return Task.CompletedTask;
    }
}
