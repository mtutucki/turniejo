namespace Turniejo.SharedKernel.Primitives;

public interface IOutboxEvent : IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}