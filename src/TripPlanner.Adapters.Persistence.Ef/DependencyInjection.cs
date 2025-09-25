using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripPlanner.Adapters.Persistence.Ef.Persistence;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;
using TripPlanner.Core.Application.Application.Abstractions;

namespace TripPlanner.Adapters.Persistence.Ef;

public static class DependencyInjection
{
    public static IServiceCollection AddEfPersistence(this IServiceCollection services, string connectionString, IHostEnvironment env)
    {
        services.AddDbContext<AppDbContext>(o =>
        {
            o.UseSqlite(connectionString);
            o.EnableSensitiveDataLogging(env.IsDevelopment()); 
        });
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<TripPlanner.Core.Application.Application.Abstractions.IUserRepository, EfUserStore>();
        services.AddScoped<TripRepository>();
        return services;
    }
}