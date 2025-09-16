using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Adapters.Persistence.Ef.Persistence;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;
using TripPlanner.Core.Application.Application.Abstractions;

namespace TripPlanner.Adapters.Persistence.Ef;

public static class DependencyInjection
{
    public static IServiceCollection AddEfPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}