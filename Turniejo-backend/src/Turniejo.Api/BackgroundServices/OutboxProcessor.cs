using MediatR;
using System.Text.Json;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Api.BackgroundServices;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    // Zdarzenia live (gol, kartka, start/koniec meczu) idą do SignalR przez outbox — przy 10 s widzowie
    // dostawali wynik z wyraźnym opóźnieniem. Zapytanie o puste outboxy co sekundę jest tanie.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Wyjątek z BackgroundService zatrzymuje cały host (BackgroundServiceExceptionBehavior.StopHost) —
                // chwilowo niedostępna baza wyłączała więc całe API. Logujemy i próbujemy w następnym cyklu.
                logger.LogError(ex, "Outbox processing cycle failed — retrying in the next cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    // Publiczne, żeby testy integracyjne mogły ręcznie "przepompować" outbox po HTTP-callu
    // zamiast czekać na 10-sekundowy interwał tła (który w hoście testowym jest wyłączony,
    // bo start przed migracjami wywala IServiceProvider).
    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
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
                    message.MarkAsFailed(ex.Message);

                    if (message.IsDeadLettered)
                    {
                        // LogCritical celowo — to jest moment, w którym wiadomość przestaje być
                        // w ogóle podejmowana przez GetPendingAsync i utyka na stałe. Bez tego
                        // przekroczenie progu retry było nieodróżnialne od zwykłego LogError.
                        logger.LogCritical(ex,
                            "Outbox message {Id} of type {Type} exceeded {MaxRetryCount} retries and is now dead-lettered — requires manual intervention",
                            message.Id, message.Type, OutboxMessage.MaxRetryCount);
                    }
                    else
                    {
                        logger.LogError(ex, "Failed to process outbox message {Id} (attempt {RetryCount}/{MaxRetryCount})",
                            message.Id, message.RetryCount, OutboxMessage.MaxRetryCount);
                    }
                }

                // SaveChanges per wiadomość, nie raz na cały batch 20 — handlery (np. tworzenie
                // Notification) nie są idempotentne, więc crash w środku batcha z jednym SaveChanges
                // na końcu powtarzał przy restarcie WSZYSTKIE już zastosowane efekty uboczne z tego
                // batcha, nie tylko tę jedną wiadomość, na której faktycznie padło.
                await repository.SaveChangesAsync(cancellationToken);
            }
        }
    }
}