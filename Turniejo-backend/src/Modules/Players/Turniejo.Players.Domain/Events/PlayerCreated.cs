using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Players.Domain.Events
{
    public sealed record PlayerCreated(
        Guid PlayerId,
        string FirstName,
        string LastName,
        Guid CreatedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
