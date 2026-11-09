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

            // Mecze bezpośrednie liczymy jako małą tabelę wszystkich drużyn z tą samą liczbą punktów.
            // Porównanie parami przy trzech drużynach (A>B, B>C, C>A) dawało cykl i przypadkową kolejność.
            var headToHead = stats.ToDictionary(
                s => s.TournamentTeamId,
                s => MiniTable(s, stats, matches, pointsForWin, pointsForDraw, pointsForLoss));

            var comparer = Comparer<TeamStats>.Create((a, b) =>
            {
                var byPoints = b.Points.CompareTo(a.Points);
                if (byPoints != 0)
                    return byPoints;

                foreach (var rule in orderedRules)
                {
                    var result = CompareByRule(a, b, rule.Type, headToHead);
                    if (result != 0)
                        return result;
                }

                // Wszystkie skonfigurowane reguły się wyczerpały bez rozstrzygnięcia (m.in. tak zawsze
                // kończy się ManualDecision — nie da się go policzyć bez udziału organizatora, dla
                // którego nie ma dziś żadnego mechanizmu zapisu decyzji). Bez tego kolejność remisujących
                // drużyn zależałaby od nieokreślonej kolejności zwróconej przez bazę (OrderBy jest
                // stabilny, ale samo wejście nie jest sortowane) — więc jako absolutny ostatni fallback
                // sortujemy po deterministycznym kluczu z ID drużyny, żeby przeliczenie tabeli zawsze
                // dawało ten sam wynik.
                return StableTieBreakKey(a.TournamentTeamId).CompareTo(StableTieBreakKey(b.TournamentTeamId));
            });

            return stats.OrderBy(s => s, comparer).Select(s => s.TournamentTeamId).ToList();
        }

        private static int CompareByRule(
            TeamStats a, TeamStats b, string ruleType, IReadOnlyDictionary<Guid, (int Points, int GoalDiff)> headToHead)
        {
            return ruleType switch
            {
                "GoalDifference" => (b.GoalsFor - b.GoalsAgainst).CompareTo(a.GoalsFor - a.GoalsAgainst),
                "GoalsFor" => b.GoalsFor.CompareTo(a.GoalsFor),
                "Wins" => b.Wins.CompareTo(a.Wins),
                "HeadToHeadPoints" => headToHead[b.TournamentTeamId].Points.CompareTo(headToHead[a.TournamentTeamId].Points),
                "HeadToHeadGoalDiff" => headToHead[b.TournamentTeamId].GoalDiff.CompareTo(headToHead[a.TournamentTeamId].GoalDiff),
                // Prawdziwe losowanie musiałoby być deterministyczne między kolejnymi przeliczeniami
                // tabeli (StandingUpdated potrafi się odpalić wielokrotnie) — inaczej kolejność
                // migałaby przy każdym przeliczeniu. Stabilny klucz z ID drużyny daje efekt losowego
                // rozstrzygnięcia bez faktycznej niedeterministyczności.
                "RandomDraw" => StableTieBreakKey(a.TournamentTeamId).CompareTo(StableTieBreakKey(b.TournamentTeamId)),
                // ManualDecision celowo zostaje no-opem na tym poziomie — wymaga realnej decyzji
                // organizatora, dla której dziś nie ma żadnego mechanizmu zapisu w domenie. Ostateczna
                // kolejność i tak nie zostanie niedeterministyczna dzięki fallbackowi w SortTeams.
                _ => 0
            };
        }

        private static int StableTieBreakKey(Guid tournamentTeamId) => tournamentTeamId.GetHashCode();

        // Punkty i bilans bramek drużyny w meczach rozegranych tylko między drużynami z tą samą liczbą punktów.
        private static (int Points, int GoalDiff) MiniTable(
            TeamStats team, IReadOnlyList<TeamStats> stats, IReadOnlyList<GroupMatchDto> matches,
            int pointsForWin, int pointsForDraw, int pointsForLoss)
        {
            var tied = stats.Where(s => s.Points == team.Points).Select(s => s.TournamentTeamId).ToHashSet();
            int points = 0, goalDiff = 0;

            foreach (var match in matches.Where(m => tied.Contains(m.HomeTournamentTeamId) && tied.Contains(m.AwayTournamentTeamId)))
            {
                int own, other;
                if (match.HomeTournamentTeamId == team.TournamentTeamId)
                    (own, other) = (match.HomeScore, match.AwayScore);
                else if (match.AwayTournamentTeamId == team.TournamentTeamId)
                    (own, other) = (match.AwayScore, match.HomeScore);
                else
                    continue;

                goalDiff += own - other;
                points += own > other ? pointsForWin : own < other ? pointsForLoss : pointsForDraw;
            }

            return (points, goalDiff);
        }
    }
}
