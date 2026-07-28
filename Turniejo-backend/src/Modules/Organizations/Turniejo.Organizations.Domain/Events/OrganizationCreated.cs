using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Organizations.Domain.Events;

public sealed record OrganizationCreated(
    Guid OrganizationId,
    Guid OwnerUserId,
    string Name) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}