using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Client;

public static class ServiceCollectionExtensions
{
    public sealed class Options { public string BaseAddress { get; set; } = "http://localhost:5162"; }

    public static IServiceCollection AddTripPlannerClient(this IServiceCollection services, Action<Options>? configure = null)
    {
        var opts = new Options();
        configure?.Invoke(opts);

        // Auth endpoints MUST use a bare HttpClient (no auth handler), so keep this:
        services.AddHttpClient<AuthClient>(c => c.BaseAddress = new Uri(opts.BaseAddress));

        // Provide a default IAuthState if the host app doesn't override it:
        services.TryAddSingleton<IAuthState, InMemoryAuthState>();

        // 🔧 The important line: register the typed HttpClient for the INTERFACE
        services.AddTransient<AuthHttpMessageHandler>();
        services.AddHttpClient<ITripPlannerClient, TripPlannerClient>(c => c.BaseAddress = new Uri(opts.BaseAddress))
            .AddHttpMessageHandler<AuthHttpMessageHandler>();

        return services;
    }
}

file sealed class InMemoryAuthState : IAuthState
{
    public string? AccessToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; private set; }
    public string? RefreshToken { get; private set; }

    public void SetTokens(string accessToken, int expiresInSeconds, string refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
    }

    public void Clear()
    {
        AccessToken = null;
        AccessTokenExpiresAt = null;
        RefreshToken = null;
    }
}