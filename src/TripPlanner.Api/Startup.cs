using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Adapters.Persistence.Ef;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Api.Auth;
using TripPlanner.Api.Endpoints;
using TripPlanner.Api.Infrastructure.Time;
using TripPlanner.Api.Swagger;
using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Application.Application.Trips;
using TripPlanner.Core.Application.Application.Trips.Commands;
using TripPlanner.Core.Application.Application.Trips.Queries;
using TripPlanner.Core.Validation.Validators;

namespace TripPlanner.Api;

// This Startup class exists to support tools (like Swashbuckle CLI) that
// expect the classic Startup pattern. The runtime app still uses minimal
// hosting in Program.cs. Keep registrations aligned with Program.cs.
public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public Startup(IConfiguration configuration, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _env = env;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Validation
        services.AddValidatorsFromAssemblyContaining<CreateTripRequestValidator>();

        // Swagger
        services.AddEndpointsApiExplorer();
        services.AddTripPlannerSwagger();

        // JSON (force camelCase)
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

        // Handlers
        services.AddScoped<CreateTripHandler>();
        services.AddScoped<ListTripsHandler>();
        services.AddScoped<GetTripByIdHandler>();
        services.AddScoped<AddParticipantHandler>();
        services.AddScoped<ProposeDateOptionHandler>();
        services.AddScoped<CastVoteHandler>();
        services.AddScoped<ListDestinationsHandler>();
        services.AddScoped<ProposeDestinationHandler>();
        services.AddScoped<VoteDestinationHandler>();

        // Persistence (EF + SQLite)
        var cs = _configuration.GetConnectionString("Default") ?? "Data Source=tripplanner.db";
        services.AddEfPersistence(cs, _env);

        // Auth and clock
        services.AddJwtAuth(_configuration);
        services.AddSingleton<IClock, SystemClock>();
    }

    public void Configure(IApplicationBuilder app)
    {
        // For CLI generation, middleware pipeline is not crucial, but
        // exposing Swagger is harmless if the app is run this way.
        app.UseSwagger();

        // Ensure database exists when running in Development, same as Program.cs
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        if (env.IsDevelopment())
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                db.Database.EnsureCreated();
            }
            catch { }
        }

        // Note: Endpoint mapping is handled in Program.cs for the running app.
        // The CLI only needs services to be registered for swagger generation.
    }
}
