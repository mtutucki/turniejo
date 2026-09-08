using MediatR;
using Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications;

namespace Turniejo.Api.BackgroundServices;

// CreateMatchReminderNotificationsCommand miał już pełną logikę (łącznie z dedupem przez
// MatchReminderExistsAsync), ale nic go nie wywoływało — przypomnienia o meczach nigdy się nie wysyłały.
public sealed class MatchReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MatchReminderBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int MinutesBeforeMatch = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MatchReminderBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await SendRemindersAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var result = await sender.Send(new CreateMatchReminderNotificationsCommand(MinutesBeforeMatch), cancellationToken);
            if (result.IsFailure)
                logger.LogError("Failed to send match reminders: {Error}", result.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled error while sending match reminders");
        }
    }
}
