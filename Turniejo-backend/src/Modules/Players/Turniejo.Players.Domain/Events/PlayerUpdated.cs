using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Players.Domain.Events
{
    public sealed record PlayerUpdated(
        Guid PlayerId,
        string FirstName,
        string LastName) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
