using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    /// <summary>Porzucony remis w pucharze został wstrzymany i czeka na rozstrzygnięcie organizatora.</summary>
    public sealed record MatchAwaitingDecision(
        Guid MatchId,
        Guid TournamentId,
        Guid HomeTournamentTeamId,
        Guid AwayTournamentTeamId,
        int HomeScore,
        int AwayScore) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
