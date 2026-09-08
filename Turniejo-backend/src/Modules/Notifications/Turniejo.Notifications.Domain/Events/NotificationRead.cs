using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Notifications.Domain.Events
{
    public sealed record NotificationRead(
        Guid NotificationRecipientId,
        Guid NotificationId,
        Guid UserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
