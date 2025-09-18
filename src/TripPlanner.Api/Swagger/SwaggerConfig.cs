using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using TripPlanner.Api.Swagger.Examples;

namespace TripPlanner.Api.Swagger;

public static class SwaggerConfig
{
    public static IServiceCollection AddTripPlannerSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TripPlanner API",
                Version = "v1",
                Description = "Simple trip planning API (trips, participants, date options, votes)."
            });

            // Works across Swashbuckle versions
            c.EnableAnnotations();

            c.SupportNonNullableReferenceTypes();

            // Map newer primitives
            c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
            c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });

            // Enable example providers
            c.ExampleFilters();
        });

        // Register example providers from this assembly
        services.AddSwaggerExamplesFromAssemblyOf<CreateTripRequestExample>();

        return services;
    }
}