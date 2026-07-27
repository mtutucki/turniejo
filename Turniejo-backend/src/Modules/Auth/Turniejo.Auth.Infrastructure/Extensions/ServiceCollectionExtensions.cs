using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Auth.Infrastructure.Identity;
using Turniejo.Auth.Infrastructure.Keycloak;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Auth.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, JwtCurrentUserContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Keycloak:Authority"];
                options.Audience = configuration["Keycloak:Audience"];
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization();

        // Keycloak Admin
        services.AddTransient<KeycloakAdminTokenHandler>();
        services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>(client =>
        {
            client.BaseAddress = new Uri(configuration["Keycloak:BaseUrl"]!);
        })
        .AddHttpMessageHandler<KeycloakAdminTokenHandler>();

        return services;
    }
}