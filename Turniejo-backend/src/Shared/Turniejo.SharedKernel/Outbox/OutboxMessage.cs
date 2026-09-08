namespace Turniejo.SharedKernel.Outbox;

public sealed class OutboxMessage
{
    // Po tylu nieudanych próbach wiadomość jest martwa na stałe (GetPendingAsync ją pomija) —
    // OutboxProcessor loguje to jako LogCritical w chwili przekroczenia progu, żeby nie utykała po
    // cichu bez żadnego alertu operacyjnego.
    public const int MaxRetryCount = 5;

    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public bool IsDeadLettered => ProcessedAt == null && RetryCount >= MaxRetryCount;

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow,
            ProcessedAt = null,
            RetryCount = 0,
            Error = null,
            NextAttemptAt = null
        };

    public void MarkAsProcessed() =>
        ProcessedAt = DateTimeOffset.UtcNow;

    public void MarkAsFailed(string error)
    {
        RetryCount++;
        Error = error;

        // Backoff wykładniczy (20s, 40s, 80s, 160s) — bez tego OutboxProcessor bił w tę samą
        // wiadomość co każdy cykl (10s) niezależnie od tego, ile razy już zawiodła, zamiast dać
        // padniętej zależności (np. SMTP, webhook) czas na powrót.
        NextAttemptAt = RetryCount < MaxRetryCount
            ? DateTimeOffset.UtcNow.AddSeconds(10 * Math.Pow(2, RetryCount))
            : null;
    }
}