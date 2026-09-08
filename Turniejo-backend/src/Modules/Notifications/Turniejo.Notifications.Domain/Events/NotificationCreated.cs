using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Notifications.Domain.Events
{
    public sealed record NotificationCreated(
        Guid NotificationId,
        Guid? TournamentId,
        string Type) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
