using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TripPlanner.Api.Swagger;

/// <summary>
/// Adds a curated list of tags with descriptions and preserves their order in Swagger UI.
/// </summary>
public sealed class TagDescriptionsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Define preferred order and descriptions
        var tags = new[]
        {
            new OpenApiTag { Name = "Auth", Description = "Registration, login, token refresh and logout" },
            new OpenApiTag { Name = "Users", Description = "User-related operations" },
            new OpenApiTag { Name = "Trips", Description = "Create trips, list mine, get details, set status, delete" },
            new OpenApiTag { Name = "Invites", Description = "Create invite codes, join trips by code" },
            new OpenApiTag { Name = "Dates", Description = "Propose date options and cast votes (self/proxy)" },
            new OpenApiTag { Name = "Destinations", Description = "Propose destinations, list, and cast votes (self/proxy)" },
            new OpenApiTag { Name = "Participants", Description = "Placeholders and claiming via one-time codes" },
        };

        swaggerDoc.Tags = tags.ToList();
    }
}
