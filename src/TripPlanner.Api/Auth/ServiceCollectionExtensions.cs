using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TripPlanner.Api.Auth;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration cfg)
    {
        var opts = new JwtOptions();
        cfg.GetSection("Jwt").Bind(opts);

        // Ensure a valid key is present even when configuration isn't loaded (e.g., during CLI swagger gen)
        string? key = opts.Key;
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            // Try common env var naming for hierarchical config first
            key = cfg["Jwt:Key"]
                  ?? Environment.GetEnvironmentVariable("Jwt__Key")
                  ?? Environment.GetEnvironmentVariable("JWT__KEY")
                  ?? "dev-only-32+chars-secret-1234567890"; // final safe fallback for tooling
        }

        // Fallbacks for issuer/audience as well to avoid nulls in validation
        var issuer = string.IsNullOrWhiteSpace(opts.Issuer) ? (cfg["Jwt:Issuer"] ?? "https://localhost") : opts.Issuer;
        var audience = string.IsNullOrWhiteSpace(opts.Audience) ? (cfg["Jwt:Audience"] ?? "tripplanner") : opts.Audience;

        // Update bound options
        opts.Key = key;
        opts.Issuer = issuer;
        opts.Audience = audience;

        services.AddSingleton(opts);
        services.AddSingleton<IJwtService, JwtService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                // Preserve original JWT claim types (keep 'sub', 'email', etc.)
                o.MapInboundClaims = false;
                o.SaveToken = false;
                o.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidIssuer = opts.Issuer,
                    ValidAudience = opts.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Key)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "sub",
#if NET8_0_OR_GREATER
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
#endif
                };
            });

        services.AddAuthorization();

        return services;
    }
}