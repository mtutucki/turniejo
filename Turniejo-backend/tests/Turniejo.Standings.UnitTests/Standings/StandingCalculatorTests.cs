using FluentAssertions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Standings.RecalculateStandings;

namespace Turniejo.Standings.UnitTests.Standings
{
    public sealed class StandingCalculatorTests
    {
        private static readonly Guid TeamA = Guid.NewGuid();
        private static readonly Guid TeamB = Guid.NewGuid();
        private static readonly Guid TeamC = Guid.NewGuid();

        [Fact]
        public void ComputeStats_ShouldAggregateAcrossHomeAndAwayMatches()
        {
            // Arrange: A beats B 2-0 (home), draws C 1-1 (away)
            var matches = new List<GroupMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), TeamA, TeamB, 2, 0),
                new(Guid.NewGuid(), Guid.NewGuid(), TeamC, TeamA, 1, 1)
            };

            // Act
            var stats = StandingCalculator.ComputeStats(TeamA, matches, pointsForWin: 3, pointsForDraw: 1, pointsForLoss: 0);

            // Assert
            stats.MatchesPlayed.Should().Be(2);
            stats.Wins.Should().Be(1);
            stats.Draws.Should().Be(1);
            stats.Losses.Should().Be(0);
            stats.GoalsFor.Should().Be(3);
            stats.GoalsAgainst.Should().Be(1);
            stats.Points.Should().Be(4);
        }

        [Fact]
        public void ComputeStats_ShouldIgnoreMatchesNotInvolvingTeam()
        {
            var matches = new List<GroupMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), TeamB, TeamC, 1, 0)
            };

            var stats = StandingCalculator.ComputeStats(TeamA, matches, 3, 1, 0);

            stats.MatchesPlayed.Should().Be(0);
        }

        [Fact]
        public void SortTeams_ShouldOrderByPointsDescending_WhenNoTiebreakerNeeded()
        {
            var stats = new List<StandingCalculator.TeamStats>
            {
                new(TeamA, 3, 1, 0, 2, 3, 5, 3),
                new(TeamB, 3, 3, 0, 0, 9, 1, 9),
                new(TeamC, 3, 0, 1, 2, 1, 8, 1)
            };

            var sorted = StandingCalculator.SortTeams(stats, [], [], 3, 1, 0);

            sorted.Should().ContainInOrder(TeamB, TeamA, TeamC);
        }

        [Fact]
        public void SortTeams_ShouldApplyGoalDifferenceTiebreaker_WhenPointsEqual()
        {
            // A and B both have 4 points; A has better goal difference.
            var stats = new List<StandingCalculator.TeamStats>
            {
                new(TeamA, 2, 1, 1, 0, 5, 2, 4),
                new(TeamB, 2, 1, 1, 0, 3, 2, 4)
            };
            var rules = new List<TieBreakerRuleDto> { new(1, "GoalDifference") };

            var sorted = StandingCalculator.SortTeams(stats, [], rules, 3, 1, 0);

            sorted.Should().ContainInOrder(TeamA, TeamB);
        }

        [Fact]
        public void SortTeams_ShouldApplyHeadToHeadPoints_WhenPointsAndGoalDiffEqual()
        {
            // A beat B head-to-head; both tied on aggregate points/goal difference for the purpose of this rule test.
            var matches = new List<GroupMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), TeamA, TeamB, 2, 1)
            };
            var rules = new List<TieBreakerRuleDto> { new(1, "HeadToHeadPoints") };

            var tiedStats = new List<StandingCalculator.TeamStats>
            {
                new(TeamA, 1, 1, 0, 0, 2, 1, 3),
                new(TeamB, 1, 1, 0, 0, 2, 1, 3)
            };

            var sorted = StandingCalculator.SortTeams(tiedStats, matches, rules, 3, 1, 0);

            sorted.Should().ContainInOrder(TeamA, TeamB);
        }

        [Fact]
        public void SortTeams_ShouldApplyHeadToHeadGoalDiff_WhenConfigured()
        {
            var matches = new List<GroupMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), TeamB, TeamA, 0, 3)
            };
            var stats = new List<StandingCalculator.TeamStats>
            {
                new(TeamA, 1, 1, 0, 0, 3, 0, 3),
                new(TeamB, 1, 1, 0, 0, 3, 0, 3)
            };
            var rules = new List<TieBreakerRuleDto> { new(1, "HeadToHeadGoalDiff") };

            var sorted = StandingCalculator.SortTeams(stats, matches, rules, 3, 1, 0);

            sorted.Should().ContainInOrder(TeamA, TeamB);
        }

        [Fact]
        public void SortTeams_ShouldFallBackToNextRule_WhenFirstRuleIsTied()
        {
            // Both A and B have GoalDifference=2 (tied), but B has more GoalsFor.
            var stats = new List<StandingCalculator.TeamStats>
            {
                new(TeamA, 2, 1, 0, 1, 5, 3, 3),
                new(TeamB, 2, 1, 0, 1, 7, 5, 3)
            };
            var rules = new List<TieBreakerRuleDto>
            {
                new(1, "GoalDifference"),
                new(2, "GoalsFor")
            };

            var sorted = StandingCalculator.SortTeams(stats, [], rules, 3, 1, 0);

            sorted.Should().ContainInOrder(TeamB, TeamA);
        }
    }
}
