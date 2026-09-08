using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Teams.Domain.Events
{
    public sealed record TeamCreated(
        Guid TeamId,
        Guid OrganizationId,
        Guid CreatedByUserId,
        string Name) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
