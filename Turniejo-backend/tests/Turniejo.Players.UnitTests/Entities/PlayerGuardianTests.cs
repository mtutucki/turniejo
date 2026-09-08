using FluentAssertions;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.Players.Domain.Events;

namespace Turniejo.Players.UnitTests.Entities
{
    public sealed class PlayerGuardianTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidPlayerId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();
        private static Guid ValidAssignedByUserId => Guid.NewGuid();

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var playerId = ValidPlayerId;
            var userId = ValidUserId;

            // Act
            var result = PlayerGuardian.Create(ValidId, playerId, userId, RelationshipType.Parent, true, false, ValidAssignedByUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PlayerId.Should().Be(playerId);
            result.Value.UserId.Should().Be(userId);
            result.Value.RelationshipType.Should().Be(RelationshipType.Parent);
            result.Value.CanEdit.Should().BeTrue();
            result.Value.CanReceiveNotifications.Should().BeFalse();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = PlayerGuardian.Create(Guid.Empty, ValidPlayerId, ValidUserId, RelationshipType.Parent, true, true, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenPlayerIdIsEmpty()
        {
            var result = PlayerGuardian.Create(ValidId, Guid.Empty, ValidUserId, RelationshipType.Parent, true, true, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
        }

        [Fact]
        public void Create_ShouldFail_WhenUserIdIsEmpty()
        {
            var result = PlayerGuardian.Create(ValidId, ValidPlayerId, Guid.Empty, RelationshipType.Parent, true, true, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.UserId");
        }

        [Fact]
        public void Create_ShouldAddGuardianAssignedToPlayerEvent()
        {
            var result = PlayerGuardian.Create(ValidId, ValidPlayerId, ValidUserId, RelationshipType.Guardian, false, true, ValidAssignedByUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is GuardianAssignedToPlayer);
        }
    }
}
