using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record MatchEventReverted(
        Guid MatchEventId,
        Guid MatchId,
        Guid TournamentId,
        Guid RevertedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
