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
        
        if (string.IsNullOrWhiteSpace(opts.Key) || Encoding.UTF8.GetByteCount(opts.Key) < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
        
        services.AddSingleton(opts);
        services.AddSingleton<IJwtService, JwtService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                // Preserve original JWT claim types (keep 'sub', 'email', etc.)
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = opts.Issuer,
                    ValidAudience = opts.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Key)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "sub"
                };
            });

        services.AddAuthorization();

        return services;
    }
}