using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Standings.Domain.Events
{
    public sealed record StandingUpdated(
        Guid TournamentId,
        Guid CategoryId,
        Guid GroupId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
