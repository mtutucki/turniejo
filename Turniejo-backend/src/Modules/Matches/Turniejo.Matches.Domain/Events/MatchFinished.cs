using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record MatchFinished(
        Guid MatchId,
        Guid TournamentId,
        Guid? GroupId,
        string ResultType,
        Guid? WinnerTournamentTeamId,
        DateTimeOffset FinishedAt,
        Guid ChangedByUserId,
        int HomeScore,
        int AwayScore) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
