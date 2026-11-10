using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.Application.TeamPlayers.GetTeamHistory
{
    /// <summary>
    /// Obliczenia historii drużyny bez dostępu do danych: wynik meczu z perspektywy drużyny, bilans
    /// i miejsce w pucharze. Remis rozstrzygnięty karnymi to zwycięstwo albo porażka (jak w drabince).
    /// </summary>
    internal static class TeamHistoryCalculator
    {
        public const string Win = "W", Draw = "D", Loss = "L";

        /// <summary>Wynik zakończonego meczu dla drużyny; null, gdy mecz się nie skończył albo w nim nie grała.</summary>
        public static string? Outcome(TournamentMatchDto m, Guid tournamentTeamId)
        {
            if (m.Status != "Finished")
                return null;
            var home = m.HomeTournamentTeamId == tournamentTeamId;
            if (!home && m.AwayTournamentTeamId != tournamentTeamId)
                return null;

            var (own, other) = home ? (m.HomeScore, m.AwayScore) : (m.AwayScore, m.HomeScore);
            if (own != other)
                return own > other ? Win : Loss;
            if (m.HomePenaltyScore is { } hp && m.AwayPenaltyScore is { } ap && hp != ap)
                return (home ? hp > ap : ap > hp) ? Win : Loss;
            return Draw;
        }

        public static TeamRecordDto Record(IEnumerable<(TournamentMatchDto Match, Guid TournamentTeamId)> matches)
        {
            int played = 0, wins = 0, draws = 0, losses = 0, goalsFor = 0, goalsAgainst = 0;
            foreach (var (m, teamId) in matches)
            {
                var outcome = Outcome(m, teamId);
                if (outcome == null)
                    continue;
                var home = m.HomeTournamentTeamId == teamId;
                played++;
                goalsFor += home ? m.HomeScore : m.AwayScore;
                goalsAgainst += home ? m.AwayScore : m.HomeScore;
                if (outcome == Win) wins++;
                else if (outcome == Draw) draws++;
                else losses++;
            }

            return new TeamRecordDto(played, wins, draws, losses, goalsFor, goalsAgainst);
        }

        public sealed record CupResult(string Kind, int? EliminatedRoundFromEnd = null);

        /// <summary>
        /// Miejsce w pucharze kategorii z węzłów drabinki (jednej kategorii) i meczów. null = drużyna nie
        /// trafiła do drabinki. Węzeł ma drużynę, gdy do niego awansowała, więc najdalszy węzeł z drużyną
        /// to jej ostatnia (albo bieżąca) runda.
        /// </summary>
        public static CupResult? Cup(
            IReadOnlyList<BracketNodeInfoDto> categoryNodes,
            IReadOnlyDictionary<Guid, TournamentMatchDto> matchesById,
            Guid tournamentTeamId)
        {
            bool Has(BracketNodeInfoDto n) =>
                n.HomeTournamentTeamId == tournamentTeamId || n.AwayTournamentTeamId == tournamentTeamId;
            string? OutcomeOf(BracketNodeInfoDto n) =>
                n.MatchId is { } id && matchesById.TryGetValue(id, out var m) ? Outcome(m, tournamentTeamId) : null;

            var standardRounds = categoryNodes.Where(n => !n.IsThirdPlace).Select(n => n.RoundNumber).DefaultIfEmpty(0).Max();
            var thirdPlace = categoryNodes.FirstOrDefault(n => n.IsThirdPlace && Has(n));
            if (thirdPlace != null)
            {
                return OutcomeOf(thirdPlace) switch
                {
                    Win => new CupResult("Third"),
                    Loss => new CupResult("Fourth"),
                    _ => new CupResult("InCup"),
                };
            }

            var deepest = categoryNodes.Where(n => !n.IsThirdPlace && Has(n)).MaxBy(n => n.RoundNumber);
            if (deepest == null)
                return null;

            var outcome = OutcomeOf(deepest);
            var isFinal = deepest.RoundNumber == standardRounds;
            if (outcome == Win && isFinal)
                return new CupResult("Champion");
            if (outcome == Loss && isFinal)
                return new CupResult("RunnerUp");
            if (outcome == Loss)
                return new CupResult("Eliminated", standardRounds - deepest.RoundNumber + 1);
            // Wygrana bez kolejnego węzła z drużyną (awans jeszcze nie wpisany) albo mecz przed nami.
            return new CupResult("InCup");
        }
    }
}
