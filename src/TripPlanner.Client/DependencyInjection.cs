using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using TripPlanner.Client.Abstractions;
using TripPlanner.Client.Configuration;

namespace TripPlanner.Client;

public static class DependencyInjection
{
    /// <summary>
    /// Registers a typed ITripPlannerClient. Reads TripPlanner:BaseAddress from IConfiguration unless overridden.
    /// </summary>
    public static IServiceCollection AddTripPlannerClient(
        this IServiceCollection services,
        Action<TripPlannerClientOptions>? configure = null)
    {
        services.AddOptions<TripPlannerClientOptions>()
            .Configure<IConfiguration>((opt, cfg) =>
            {
                opt.BaseAddress ??= cfg.GetSection("TripPlanner")["BaseAddress"] ?? "http://localhost:5162";
            });

        if (configure is not null) services.PostConfigure(configure);

        services.AddHttpClient<ITripPlannerClient, TripPlannerClient>((sp, http) =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TripPlannerClientOptions>>().Value;
                http.BaseAddress = new Uri(opts.BaseAddress, UriKind.Absolute);

                // TODO: auth header if added
                // http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            })
            // Optional: simple resilience policy (retry on transient 5xx & timeouts)
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        => HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(3, retry => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retry)));
}