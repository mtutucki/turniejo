using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.CreatePlayer;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Infrastructure.Persistence;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Players.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlayersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PlayersDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "players");
                    npgsql.MigrationsAssembly(typeof(PlayersDbContext).Assembly.FullName);
                }));

        services.AddScoped<IPlayersDbContext>(sp => sp.GetRequiredService<PlayersDbContext>());

        services.AddScoped<IPlayerModule, PlayerModule>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddScoped<GuardianAccessChecker>();
        services.AddScoped<MyChildMatchResolver>();
        services.AddScoped<SquadEditGuard>();
        services.AddScoped<PlayerAccessGuard>();

        services.AddValidatorsFromAssemblyContaining<CreatePlayerCommandValidator>();

        return services;
    }
}
