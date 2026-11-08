using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Results;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.Application.Matches
{
    /// <summary>Wynik meczu policzony z zapisanych goli. Samobój liczy się przeciwnikowi.</summary>
    internal static class MatchGoalScore
    {
        public static (int Home, int Away) FromEvents(Match match, IEnumerable<MatchEvent> events)
        {
            var home = 0;
            var away = 0;

            foreach (var matchEvent in events)
            {
                if (matchEvent.Type is not (MatchEventType.Goal or MatchEventType.OwnGoal or MatchEventType.PenaltyGoal))
                    continue;

                var scoringTeamId = matchEvent.Type == MatchEventType.OwnGoal
                    ? OpposingTeam(match, matchEvent.TournamentTeamId)
                    : matchEvent.TournamentTeamId;

                if (scoringTeamId == match.HomeTournamentTeamId)
                    home++;
                else if (scoringTeamId == match.AwayTournamentTeamId)
                    away++;
            }

            return (home, away);
        }

        /// <summary>Sprawdza wynik po planowanej zmianie goli, zanim cokolwiek zostanie zapisane.</summary>
        public static async Task<Result> EnsureKnockoutStaysDecidedAsync(
            IMatchesDbContext dbContext, Match match,
            Func<IReadOnlyList<MatchEvent>, IEnumerable<MatchEvent>> applyChange, CancellationToken cancellationToken)
        {
            if (match.Status != MatchStatus.Finished)
                return Result.Success();

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var (home, away) = FromEvents(match, applyChange(events));
            return match.EnsureKnockoutStaysDecided(home, away);
        }

        private static Guid? OpposingTeam(Match match, Guid? teamId)
        {
            if (teamId == match.HomeTournamentTeamId)
                return match.AwayTournamentTeamId;

            if (teamId == match.AwayTournamentTeamId)
                return match.HomeTournamentTeamId;

            return null;
        }
    }
}
