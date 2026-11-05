using Microsoft.OpenApi.Models;

namespace Turniejo.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Turniejo API",
                Version = "v1",
                Description = "Modular Monolith tournament-management backend — Users, Auth, Organizations, " +
                              "Tournaments, Teams, Players, Matches, Standings, Brackets, Notifications."
            });

            // Kontrolery nie mają [Authorize] — autoryzacja idzie przez AuthorizationBehavior w
            // pipeline MediatR, więc Swagger nic o niej nie wie z samej sygnatury endpointu. Bearer
            // schemat dajemy globalnie, żeby dało się przetestować dowolny endpoint z realnym JWT
            // z Keycloaka, bez ręcznego oznaczania każdej akcji.
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Wklej sam token JWT z Keycloaka (bez przedrostka \"Bearer \")."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    []
                }
            });

            // Wiele modułów ma osobne request/response recordy o tej samej nazwie (np. CreateXxxRequest
            // w różnych kontrolerach) — bez tego Swashbuckle rzuca konflikt przy generowaniu schematu.
            options.CustomSchemaIds(type => type.FullName);
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Turniejo API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
