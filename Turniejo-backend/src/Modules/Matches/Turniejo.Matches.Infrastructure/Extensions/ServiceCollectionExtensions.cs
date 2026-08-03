using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Matches.Infrastructure.Persistence;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMatchesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MatchesDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory", "matches");
                    npgsql.MigrationsAssembly(typeof(MatchesDbContext).Assembly.FullName);
                }));

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }
}