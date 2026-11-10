using FluentAssertions;
using Turniejo.Players.Application.TeamPlayers.GetTeamHistory;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TeamPlayers
{
    public sealed class TeamHistoryCalculatorTests
    {
        private static readonly Guid Us = Guid.NewGuid();
        private static readonly Guid Them = Guid.NewGuid();
        private static readonly Guid Category = Guid.NewGuid();

        private static TournamentMatchDto Match(Guid home, Guid away, int homeScore, int awayScore,
            string status = "Finished", int? homePen = null, int? awayPen = null) =>
            new(Guid.NewGuid(), Guid.NewGuid(), status, DateTimeOffset.UtcNow, home, away, homeScore, awayScore,
                "Knockout", null, Category, homePen, awayPen);

        private static BracketNodeInfoDto Node(int round, TournamentMatchDto? match, bool thirdPlace = false) =>
            new(Guid.NewGuid(), round, thirdPlace, match?.Id, null, null, false,
                CategoryId: Category,
                HomeTournamentTeamId: match?.HomeTournamentTeamId,
                AwayTournamentTeamId: match?.AwayTournamentTeamId);

        [Fact]
        public void Outcome_IsFromTheTeamsSide_AndPenaltiesDecideADraw()
        {
            TeamHistoryCalculator.Outcome(Match(Us, Them, 2, 1), Us).Should().Be("W");
            TeamHistoryCalculator.Outcome(Match(Them, Us, 2, 1), Us).Should().Be("L");
            TeamHistoryCalculator.Outcome(Match(Us, Them, 1, 1), Us).Should().Be("D");
            TeamHistoryCalculator.Outcome(Match(Them, Us, 1, 1, homePen: 3, awayPen: 4), Us).Should().Be("W");
            TeamHistoryCalculator.Outcome(Match(Us, Them, 1, 0, status: "Live"), Us).Should().BeNull();
        }

        [Fact]
        public void Record_CountsOnlyFinishedMatches()
        {
            var record = TeamHistoryCalculator.Record(new[]
            {
                (Match(Us, Them, 3, 0), Us),
                (Match(Them, Us, 2, 2), Us),
                (Match(Us, Them, 0, 5, status: "Live"), Us),
            });

            record.Should().Be(new Turniejo.Players.Application.DTOs.TeamRecordDto(2, 1, 1, 0, 5, 2));
        }

        private static TeamHistoryCalculator.CupResult? Cup(params (BracketNodeInfoDto Node, TournamentMatchDto? Match)[] nodes) =>
            TeamHistoryCalculator.Cup(
                nodes.Select(n => n.Node).ToList(),
                nodes.Where(n => n.Match != null).ToDictionary(n => n.Match!.Id, n => n.Match!),
                Us);

        [Fact]
        public void Cup_FinalDecidesChampionAndRunnerUp()
        {
            var semi = Match(Us, Them, 2, 0);
            var final = Match(Them, Us, 1, 1, homePen: 2, awayPen: 4);
            Cup((Node(1, semi), semi), (Node(2, final), final))!.Kind.Should().Be("Champion");

            var lostFinal = Match(Us, Them, 0, 1);
            Cup((Node(1, semi), semi), (Node(2, lostFinal), lostFinal))!.Kind.Should().Be("RunnerUp");
        }

        [Fact]
        public void Cup_LostSemiFinal_IsEliminatedTwoRoundsFromTheEnd_UnlessThereIsAThirdPlaceMatch()
        {
            var quarter = Match(Us, Them, 1, 0);
            var semi = Match(Them, Us, 3, 1);
            var otherFinal = Node(3, null);
            var result = Cup((Node(1, quarter), quarter), (Node(2, semi), semi), (otherFinal, null))!;
            result.Kind.Should().Be("Eliminated");
            result.EliminatedRoundFromEnd.Should().Be(2);

            var bronze = Match(Us, Them, 2, 1);
            Cup((Node(2, semi), semi), (otherFinal, null), (Node(3, bronze, thirdPlace: true), bronze))!
                .Kind.Should().Be("Third");
            var lostBronze = Match(Us, Them, 0, 1);
            Cup((Node(2, semi), semi), (otherFinal, null), (Node(3, lostBronze, thirdPlace: true), lostBronze))!
                .Kind.Should().Be("Fourth");
        }

        [Fact]
        public void Cup_IsNull_WhenTheTeamIsNotInTheBracket_AndInCupWhileStillPlaying()
        {
            var other = Match(Guid.NewGuid(), Them, 1, 0);
            Cup((Node(1, other), other)).Should().BeNull();

            var upcoming = Match(Us, Them, 0, 0, status: "Scheduled");
            Cup((Node(1, upcoming), upcoming), (Node(2, null), null))!.Kind.Should().Be("InCup");
        }
    }
}
