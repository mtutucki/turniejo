using MediatR;
using System.Text.Json;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Api.BackgroundServices;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingMessagesAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repositories = scope.ServiceProvider
            .GetServices<IOutboxRepository>();
        var publisher = scope.ServiceProvider
            .GetRequiredService<IPublisher>();

        foreach (var repository in repositories)
        {
            var messages = await repository.GetPendingAsync(
                batchSize: 20,
                cancellationToken: cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    var type = Type.GetType(message.Type);
                    if (type is null)
                    {
                        logger.LogWarning("Unknown outbox message type: {Type}", message.Type);
                        continue;
                    }

                    var domainEvent = JsonSerializer.Deserialize(message.Payload, type);
                    if (domainEvent is IDomainEvent @event)
                        await publisher.Publish(@event, cancellationToken);

                    message.MarkAsProcessed();
                    logger.LogInformation("Processed outbox message {Id} of type {Type}",
                        message.Id, message.Type);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                    message.MarkAsFailed(ex.Message);
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}