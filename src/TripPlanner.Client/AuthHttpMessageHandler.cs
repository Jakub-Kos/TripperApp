using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TripPlanner.Client;

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

        // retry once with new token
        var retry = request.Clone(); // extension below
        await AttachAccessAsync(retry, ct);
        return await base.SendAsync(retry, ct);
    }

    private Task AttachAccessAsync(HttpRequestMessage req, CancellationToken _)
    {
        if (!string.IsNullOrWhiteSpace(_state.AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _state.AccessToken);
        return Task.CompletedTask;
    }
}

file static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage Clone(this HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        // copy headers & content
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (req.Content is not null)
        {
            var ms = new MemoryStream();
            req.Content.CopyTo(ms, null, CancellationToken.None);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return clone;
    }
}
