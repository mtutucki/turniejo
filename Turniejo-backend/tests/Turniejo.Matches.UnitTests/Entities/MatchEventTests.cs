using FluentAssertions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Matches.UnitTests.Entities
{
    public sealed class MatchEventTests
    {
        [Fact]
        public void Create_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            // Act
            var result = MatchEvent.Create(Guid.NewGuid(), matchId, MatchEventType.Goal, 23, userId, tournamentTeamId, playerId, null, 15);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchId.Should().Be(matchId);
            result.Value.Type.Should().Be(MatchEventType.Goal);
            result.Value.TournamentTeamId.Should().Be(tournamentTeamId);
            result.Value.PlayerId.Should().Be(playerId);
            result.Value.Minute.Should().Be(23);
            result.Value.Second.Should().Be(15);
            result.Value.CreatedByUserId.Should().Be(userId);
            result.Value.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = MatchEvent.Create(Guid.Empty, Guid.NewGuid(), MatchEventType.MatchStarted, 0, Guid.NewGuid());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenMatchIdIsEmpty()
        {
            var result = MatchEvent.Create(Guid.NewGuid(), Guid.Empty, MatchEventType.MatchStarted, 0, Guid.NewGuid());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.MatchId");
        }

        [Fact]
        public void Create_ShouldFail_WhenMinuteIsNegative()
        {
            var result = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, -1, Guid.NewGuid());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Minute");
        }

        [Fact]
        public void Create_ShouldFail_WhenSecondIsOutOfRange()
        {
            var result = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, 10, Guid.NewGuid(), second: 60);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Second");
        }

        [Fact]
        public void MarkDeleted_ShouldSetIsDeletedAndAuditFields()
        {
            // Arrange
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.MatchStarted, 0, Guid.NewGuid()).Value;
            var deletedByUserId = Guid.NewGuid();

            // Act
            var result = matchEvent.MarkDeleted(deletedByUserId, Guid.NewGuid());

            // Assert
            result.IsSuccess.Should().BeTrue();
            matchEvent.IsDeleted.Should().BeTrue();
            matchEvent.DeletedByUserId.Should().Be(deletedByUserId);
            matchEvent.DeletedAt.Should().NotBeNull();
            matchEvent.DomainEvents.Should().ContainSingle(e => e is MatchEventReverted);
        }

        [Fact]
        public void MarkDeleted_ShouldFail_WhenAlreadyDeleted()
        {
            // Arrange
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, 10, Guid.NewGuid()).Value;
            matchEvent.MarkDeleted(Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = matchEvent.MarkDeleted(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.IsDeleted");
        }

        [Fact]
        public void UpdateDetails_ShouldUpdateFieldsAndRaiseEvent_WhenValid()
        {
            // Arrange
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, 10, Guid.NewGuid()).Value;
            var newPlayerId = Guid.NewGuid();
            var newRelatedPlayerId = Guid.NewGuid();

            // Act
            var result = matchEvent.UpdateDetails(MatchEventType.OwnGoal, 25, 30, newPlayerId, newRelatedPlayerId, Guid.NewGuid(), Guid.NewGuid());

            // Assert
            result.IsSuccess.Should().BeTrue();
            matchEvent.Type.Should().Be(MatchEventType.OwnGoal);
            matchEvent.Minute.Should().Be(25);
            matchEvent.Second.Should().Be(30);
            matchEvent.PlayerId.Should().Be(newPlayerId);
            matchEvent.RelatedPlayerId.Should().Be(newRelatedPlayerId);
            matchEvent.DomainEvents.Should().ContainSingle(e => e is MatchEventUpdated);
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenAlreadyDeleted()
        {
            // Arrange
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, 10, Guid.NewGuid()).Value;
            matchEvent.MarkDeleted(Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = matchEvent.UpdateDetails(MatchEventType.Goal, 20, null, null, null, Guid.NewGuid(), Guid.NewGuid());

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.IsDeleted");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenMinuteIsNegative()
        {
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, 10, Guid.NewGuid()).Value;
            var result = matchEvent.UpdateDetails(MatchEventType.Goal, -5, null, null, null, Guid.NewGuid(), Guid.NewGuid());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Minute");
        }
    }
}
