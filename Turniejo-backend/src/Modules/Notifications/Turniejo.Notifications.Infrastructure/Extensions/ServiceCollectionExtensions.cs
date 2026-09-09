using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Infrastructure.Persistence;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Notifications.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications");
                    npgsql.MigrationsAssembly(typeof(NotificationsDbContext).Assembly.FullName);

                }));

        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<NotificationPublisher>();
        services.AddScoped<MatchNotificationRecipientResolver>();
        services.AddScoped<TournamentParticipantResolver>();

        return services;
    }
}
