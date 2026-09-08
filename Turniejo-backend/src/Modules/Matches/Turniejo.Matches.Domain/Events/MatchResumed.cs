using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record MatchResumed(
        Guid MatchId,
        Guid TournamentId,
        Guid ChangedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
