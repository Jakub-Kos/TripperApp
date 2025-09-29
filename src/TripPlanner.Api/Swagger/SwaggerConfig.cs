using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using TripPlanner.Api.Swagger.Examples;

namespace TripPlanner.Api.Swagger;

/// <summary>
/// Helpers for registering and configuring Swagger/OpenAPI for the TripPlanner API.
/// Keeps all swagger-related knobs in one place.
/// </summary>
public static class SwaggerConfig
{
    /// <summary>
    /// Registers and configures Swagger generation, security, examples, and schema mappings.
    /// </summary>
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
            
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [ new OpenApiSecurityScheme { Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" } } ] = Array.Empty<string>()
            });

            // Works across Swashbuckle versions
            c.EnableAnnotations();

            c.SupportNonNullableReferenceTypes();

            // Map newer primitives
            c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
            c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });

            // Show nicer tag grouping in Swagger by defining tag descriptions and order
            c.DocumentFilter<TagDescriptionsDocumentFilter>();

            // Enable example providers
            c.ExampleFilters();
        });

        // Register example providers from this assembly
        services.AddSwaggerExamplesFromAssemblyOf<CreateTripRequestExample>();

        return services;
    }
}