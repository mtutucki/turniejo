using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Infrastructure.Persistence;

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

        services.AddScoped<ITournamentsDbContext>(sp =>
            sp.GetRequiredService<TournamentsDbContext>());

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddValidatorsFromAssemblyContaining<CreateTournamentCommandValidator>();

        return services;
    }
}