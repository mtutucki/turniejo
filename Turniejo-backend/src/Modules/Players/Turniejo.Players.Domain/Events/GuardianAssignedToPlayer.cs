using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Players.Domain.Events
{
    public sealed record GuardianAssignedToPlayer(
        Guid PlayerGuardianId,
        Guid PlayerId,
        Guid UserId,
        Guid AssignedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
