using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Core.Application.Application.Abstractions;

namespace TripPlanner.Adapters.Persistence.InMemory;

public static class DependencyInjection
{
    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<ITripRepository, Persistence.InMemoryTripRepository>();
        services.AddSingleton<IUnitOfWork, Persistence.InMemoryTripRepository>();
        return services;
    }
}