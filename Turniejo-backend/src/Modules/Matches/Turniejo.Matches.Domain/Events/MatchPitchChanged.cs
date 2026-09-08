using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record MatchPitchChanged(
        Guid MatchId,
        Guid TournamentId,
        Guid? PitchId,
        Guid ChangedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
