using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Standings.Application.Standings.RecalculateStandings
{
    internal static class StandingCalculator
    {
        public sealed record TeamStats(
            Guid TournamentTeamId, int MatchesPlayed, int Wins, int Draws, int Losses,
            int GoalsFor, int GoalsAgainst, int Points);

        public static TeamStats ComputeStats(
            Guid tournamentTeamId, IReadOnlyList<GroupMatchDto> matches,
            int pointsForWin, int pointsForDraw, int pointsForLoss)
        {
            int played = 0, wins = 0, draws = 0, losses = 0, goalsFor = 0, goalsAgainst = 0;

            foreach (var match in matches)
            {
                bool isHome;
                if (match.HomeTournamentTeamId == tournamentTeamId)
                    isHome = true;
                else if (match.AwayTournamentTeamId == tournamentTeamId)
                    isHome = false;
                else
                    continue;

                played++;
                var myGoals = isHome ? match.HomeScore : match.AwayScore;
                var opponentGoals = isHome ? match.AwayScore : match.HomeScore;
                goalsFor += myGoals;
                goalsAgainst += opponentGoals;

                if (myGoals > opponentGoals)
                    wins++;
                else if (myGoals == opponentGoals)
                    draws++;
                else
                    losses++;
            }

            var points = wins * pointsForWin + draws * pointsForDraw + losses * pointsForLoss;

            return new TeamStats(tournamentTeamId, played, wins, draws, losses, goalsFor, goalsAgainst, points);
        }

        public static IReadOnlyList<Guid> SortTeams(
            IReadOnlyList<TeamStats> stats, IReadOnlyList<GroupMatchDto> matches,
            IReadOnlyList<TieBreakerRuleDto> tieBreakerRules,
            int pointsForWin, int pointsForDraw, int pointsForLoss)
        {
            var orderedRules = tieBreakerRules.OrderBy(r => r.Order).ToList();

            var comparer = Comparer<TeamStats>.Create((a, b) =>
            {
                var byPoints = b.Points.CompareTo(a.Points);
                if (byPoints != 0)
                    return byPoints;

                foreach (var rule in orderedRules)
                {
                    var result = CompareByRule(a, b, rule.Type, matches, pointsForWin, pointsForDraw, pointsForLoss);
                    if (result != 0)
                        return result;
                }

                return 0;
            });

            return stats.OrderBy(s => s, comparer).Select(s => s.TournamentTeamId).ToList();
        }

        private static int CompareByRule(
            TeamStats a, TeamStats b, string ruleType, IReadOnlyList<GroupMatchDto> matches,
            int pointsForWin, int pointsForDraw, int pointsForLoss)
        {
            return ruleType switch
            {
                "GoalDifference" => (b.GoalsFor - b.GoalsAgainst).CompareTo(a.GoalsFor - a.GoalsAgainst),
                "GoalsFor" => b.GoalsFor.CompareTo(a.GoalsFor),
                "Wins" => b.Wins.CompareTo(a.Wins),
                "HeadToHeadPoints" => CompareHeadToHead(a, b, matches, pointsForWin, pointsForDraw, pointsForLoss, byGoalDiff: false),
                "HeadToHeadGoalDiff" => CompareHeadToHead(a, b, matches, pointsForWin, pointsForDraw, pointsForLoss, byGoalDiff: true),
                _ => 0
            };
        }

        private static int CompareHeadToHead(
            TeamStats a, TeamStats b, IReadOnlyList<GroupMatchDto> matches,
            int pointsForWin, int pointsForDraw, int pointsForLoss, bool byGoalDiff)
        {
            var headToHeadMatches = matches
                .Where(m =>
                    (m.HomeTournamentTeamId == a.TournamentTeamId && m.AwayTournamentTeamId == b.TournamentTeamId) ||
                    (m.HomeTournamentTeamId == b.TournamentTeamId && m.AwayTournamentTeamId == a.TournamentTeamId))
                .ToList();

            if (headToHeadMatches.Count == 0)
                return 0;

            int aPoints = 0, bPoints = 0, aGoals = 0, bGoals = 0;

            foreach (var match in headToHeadMatches)
            {
                var (aGoalsInMatch, bGoalsInMatch) = match.HomeTournamentTeamId == a.TournamentTeamId
                    ? (match.HomeScore, match.AwayScore)
                    : (match.AwayScore, match.HomeScore);

                aGoals += aGoalsInMatch;
                bGoals += bGoalsInMatch;

                if (aGoalsInMatch > bGoalsInMatch)
                {
                    aPoints += pointsForWin;
                    bPoints += pointsForLoss;
                }
                else if (aGoalsInMatch < bGoalsInMatch)
                {
                    bPoints += pointsForWin;
                    aPoints += pointsForLoss;
                }
                else
                {
                    aPoints += pointsForDraw;
                    bPoints += pointsForDraw;
                }
            }

            return byGoalDiff ? bGoals.CompareTo(aGoals) : bPoints.CompareTo(aPoints);
        }
    }
}
