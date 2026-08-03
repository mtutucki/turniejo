using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Infrastructure.Persistence;

namespace Turniejo.Teams.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TeamsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "teams");
                    npgsql.MigrationsAssembly(typeof(TeamsDbContext).Assembly.FullName);
                }));

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }
}