using FluentAssertions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.UnitTests.Entities
{
    public sealed class BracketNodeTests
    {
        [Fact]
        public void Create_ShouldSucceed_WithFixedSourcesOnBothSides()
        {
            // Arrange
            var homeTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();

            // Act
            var result = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, homeTeamId,
                BracketSourceType.Fixed, null, null, null, awayTeamId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeTournamentTeamId.Should().Be(homeTeamId);
            result.Value.AwayTournamentTeamId.Should().Be(awayTeamId);
            result.Value.IsFullyResolved.Should().BeTrue();
        }

        [Fact]
        public void Create_ShouldSucceed_WithGroupPositionSource_AndLeaveUnresolved()
        {
            var result = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.GroupPosition, Guid.NewGuid(), 1, null, null,
                BracketSourceType.GroupPosition, Guid.NewGuid(), 2, null, null);

            result.IsSuccess.Should().BeTrue();
            result.Value.IsFullyResolved.Should().BeFalse();
        }

        [Fact]
        public void Create_ShouldFail_WhenFixedSourceMissingTeamId()
        {
            var result = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, null,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.HomeTournamentTeamId");
        }

        [Fact]
        public void Create_ShouldFail_WhenGroupPositionSourceMissingGroupId()
        {
            var result = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.GroupPosition, null, 1, null, null,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.HomeSourceGroupId");
        }

        [Fact]
        public void Create_ShouldFail_WhenMatchWinnerSourceMissingSourceNode()
        {
            var result = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.MatchWinner, null, null, null, null,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.HomeSourceNodeId");
        }

        [Fact]
        public void ResolveSlot_ShouldSetHomeOrAwayTeam()
        {
            // Arrange
            var node = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null,
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null).Value;
            var teamId = Guid.NewGuid();

            // Act
            var result = node.ResolveSlot(BracketSlot.Home, teamId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            node.HomeTournamentTeamId.Should().Be(teamId);
            node.IsFullyResolved.Should().BeFalse();
        }

        [Fact]
        public void AssignMatch_ShouldFail_WhenNotFullyResolved()
        {
            var node = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null,
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null).Value;

            var result = node.AssignMatch(Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.IsFullyResolved");
        }

        [Fact]
        public void AssignMatch_ShouldSucceed_WhenFullyResolvedAndNoMatchYet()
        {
            var node = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;
            var matchId = Guid.NewGuid();

            var result = node.AssignMatch(matchId);

            result.IsSuccess.Should().BeTrue();
            node.MatchId.Should().Be(matchId);
        }

        [Fact]
        public void AssignMatch_ShouldFail_WhenAlreadyAssigned()
        {
            var node = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;
            node.AssignMatch(Guid.NewGuid());

            var result = node.AssignMatch(Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.MatchId");
        }

        [Fact]
        public void WireAdvancement_ShouldSetAllFourFields()
        {
            var node = BracketNode.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;
            var winnerNodeId = Guid.NewGuid();
            var loserNodeId = Guid.NewGuid();

            node.WireAdvancement(winnerNodeId, BracketSlot.Home, loserNodeId, BracketSlot.Away);

            node.WinnerGoesToNodeId.Should().Be(winnerNodeId);
            node.WinnerGoesToSlot.Should().Be(BracketSlot.Home);
            node.LoserGoesToNodeId.Should().Be(loserNodeId);
            node.LoserGoesToSlot.Should().Be(BracketSlot.Away);
        }
    }
}
