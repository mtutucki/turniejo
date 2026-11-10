using MediatR;
using Turniejo.Matches.Application.Matches.FinishAbandonedMatches;

namespace Turniejo.Api.BackgroundServices;

// Kończy mecze, których nikt nie zamknął (zasady w FinishAbandonedMatchesCommandHandler).
public sealed class AbandonedMatchBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AbandonedMatchBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await FinishAbandonedMatchesAsync(stoppingToken);

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

    private async Task FinishAbandonedMatchesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var result = await sender.Send(new FinishAbandonedMatchesCommand(DateTimeOffset.UtcNow), cancellationToken);
            if (result.IsFailure)
                logger.LogError("Failed to finish abandoned matches: {Error}", result.Error.Message);
            else if (result.Value.FinishedCount > 0)
                logger.LogInformation("Finished {Count} abandoned matches", result.Value.FinishedCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled error while finishing abandoned matches");
        }
    }
}
