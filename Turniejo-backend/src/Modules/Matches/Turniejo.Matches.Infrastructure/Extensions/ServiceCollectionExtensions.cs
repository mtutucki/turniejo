using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Pitches.AddPitch;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Infrastructure.Persistence;
using Turniejo.Matches.Infrastructure.Realtime;
using Turniejo.SharedKernel.Contracts;
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

        services.AddScoped<IMatchesDbContext>(sp => sp.GetRequiredService<MatchesDbContext>());

        services.AddScoped<IMatchModule, MatchModule>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddValidatorsFromAssemblyContaining<AddPitchCommandValidator>();

        services.AddScoped<RealtimeAccessGuard>();
        services.AddScoped<MatchEventRosterGuard>();
        services.AddScoped<MatchAccessGuard>();
        services.AddScoped<ScheduleConflictChecker>();
        services.AddScoped<PlannedSlotShifter>();
        services.AddScoped<PlannedMatchContext>();
        services.AddScoped<SendOffRules>();
        services.AddScoped<RefereeIdVisibility>();

        services.AddSignalR();
        services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SubUserIdProvider>();
        services.AddSingleton<IRealtimeService, SignalRRealtimeService>();

        return services;
    }
}
