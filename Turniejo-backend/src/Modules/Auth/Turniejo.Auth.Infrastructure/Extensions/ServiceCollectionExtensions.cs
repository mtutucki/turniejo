using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turniejo.Auth.Infrastructure.Identity;
using Turniejo.Auth.Infrastructure.Keycloak;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Auth.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, JwtCurrentUserContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Keycloak:Authority"];
                options.Audience = configuration["Keycloak:Audience"];
                // Poza Development metadane OIDC/JWKS muszą przyjść po HTTPS — inaczej podmiana
                // kluczy walidujących JWT (np. przy błędnej konfiguracji Authority albo MITM
                // w sieci wewnętrznej) przechodzi bez żadnego szyfrowania.
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                };

                // SignalR (WebSocket/SSE) nie może ustawić nagłówka Authorization na natywnym
                // handshake — klient przekazuje token przez query string (?access_token=...),
                // co jest domyślnym zachowaniem SignalR JS/.NET client z accessTokenFactory.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;

                        return Task.CompletedTask;
                    }
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