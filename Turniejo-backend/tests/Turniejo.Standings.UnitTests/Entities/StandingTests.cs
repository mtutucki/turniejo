using FluentAssertions;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.UnitTests.Entities
{
    public sealed class StandingTests
    {
        [Fact]
        public void Create_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var teamId = Guid.NewGuid();

            // Act
            var result = Standing.Create(Guid.NewGuid(), tournamentId, categoryId, groupId, teamId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentId.Should().Be(tournamentId);
            result.Value.GroupId.Should().Be(groupId);
            result.Value.TournamentTeamId.Should().Be(teamId);
            result.Value.MatchesPlayed.Should().Be(0);
            result.Value.Points.Should().Be(0);
        }

        [Fact]
        public void Create_ShouldFail_WhenGroupIdIsEmpty()
        {
            var result = Standing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.GroupId");
        }

        [Fact]
        public void UpdateStats_ShouldSetFieldsAndComputeGoalDifference()
        {
            // Arrange
            var standing = Standing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;

            // Act
            var result = standing.UpdateStats(matchesPlayed: 3, wins: 2, draws: 1, losses: 0, goalsFor: 7, goalsAgainst: 3, points: 7);

            // Assert
            result.IsSuccess.Should().BeTrue();
            standing.MatchesPlayed.Should().Be(3);
            standing.Wins.Should().Be(2);
            standing.GoalDifference.Should().Be(4);
            standing.Points.Should().Be(7);
        }

        [Fact]
        public void UpdateStats_ShouldFail_WhenNegative()
        {
            var standing = Standing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            var result = standing.UpdateStats(-1, 0, 0, 0, 0, 0, 0);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Stats");
        }

        [Fact]
        public void SetPosition_ShouldSetPosition()
        {
            var standing = Standing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            standing.SetPosition(2);
            standing.Position.Should().Be(2);
        }
    }
}
