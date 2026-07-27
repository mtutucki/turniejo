using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Infrastructure.Persistence;
using Turniejo.Users.Infrastructure.Persistence;

namespace Turniejo.Tournaments.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTournamentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TournamentsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tournaments");
                    npgsql.MigrationsAssembly(typeof(TournamentsDbContext).Assembly.FullName);
                }));

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }
}