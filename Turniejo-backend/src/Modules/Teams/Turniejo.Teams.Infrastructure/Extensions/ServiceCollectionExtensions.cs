using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Services;
using Turniejo.Teams.Application.Teams.CreateTeam;
using Turniejo.Teams.Infrastructure.Persistence;
using Turniejo.Teams.Infrastructure.Storage;

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

        services.AddScoped<ITeamsDbContext>(sp => sp.GetRequiredService<TeamsDbContext>());

        services.AddScoped<ITeamModule, TeamModule>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddScoped<TeamMatchResolver>();

        services.AddSingleton<ITeamLogoStorage>(new TeamLogoFileStorage(TeamLogoFileStorage.ResolveUploadsRoot(configuration)));

        services.AddValidatorsFromAssemblyContaining<CreateTeamCommandValidator>();

        return services;
    }
}
