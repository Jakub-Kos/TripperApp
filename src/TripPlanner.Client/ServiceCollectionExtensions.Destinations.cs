using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace TripPlanner.Client;


public static class DestinationsClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDestinationsApi"/> using the same BaseAddress and auth handler pipeline.
    /// Call this *after* AddTripPlannerClient in WPF.
    /// </summary>
    public static IServiceCollection AddDestinationsClient(this IServiceCollection services)
    {
        services.AddHttpClient<IDestinationsApi, DestinationsClient>("TripPlanner", (sp, http) =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var baseAddr = cfg.GetSection("TripPlanner")["BaseAddress"] ?? "http://localhost:5162";
                http.BaseAddress = new Uri(baseAddr);
            })
            .AddHttpMessageHandler<AuthHttpMessageHandler>();


        return services;
    }
}