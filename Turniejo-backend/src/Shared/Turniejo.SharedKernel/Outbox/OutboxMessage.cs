namespace Turniejo.SharedKernel.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }

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
            Error = null
        };

    public void MarkAsProcessed() =>
        ProcessedAt = DateTimeOffset.UtcNow;

    public void MarkAsFailed(string error)
    {
        RetryCount++;
        Error = error;
    }
}