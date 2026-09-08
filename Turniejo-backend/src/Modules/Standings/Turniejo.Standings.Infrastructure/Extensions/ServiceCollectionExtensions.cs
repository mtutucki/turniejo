using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Infrastructure.Persistence;

namespace Turniejo.Standings.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStandingsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<StandingsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "standings");
                    npgsql.MigrationsAssembly(typeof(StandingsDbContext).Assembly.FullName);
                }));

        services.AddScoped<IStandingsDbContext>(sp => sp.GetRequiredService<StandingsDbContext>());

        services.AddScoped<IStandingModule, StandingModule>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }
}
