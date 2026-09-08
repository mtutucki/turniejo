using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Brackets.Domain.Events
{
    public sealed record BracketUpdated(Guid BracketId, Guid TournamentId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
