using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record MatchScoreChanged(
        Guid MatchId,
        Guid TournamentId,
        Guid? GroupId,
        int HomeScore,
        int AwayScore) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
