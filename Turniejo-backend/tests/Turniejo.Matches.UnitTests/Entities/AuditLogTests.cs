using FluentAssertions;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.UnitTests.Entities
{
    public sealed class AuditLogTests
    {
        [Fact]
        public void Create_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var entityId = Guid.NewGuid();

            // Act
            var result = AuditLog.Create(Guid.NewGuid(), userId, "MatchEvent", entityId, "Revert", "{\"IsDeleted\":false}", "{\"IsDeleted\":true}");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.UserId.Should().Be(userId);
            result.Value.EntityType.Should().Be("MatchEvent");
            result.Value.EntityId.Should().Be(entityId);
            result.Value.Action.Should().Be("Revert");
            result.Value.OldValue.Should().Be("{\"IsDeleted\":false}");
            result.Value.NewValue.Should().Be("{\"IsDeleted\":true}");
        }

        [Fact]
        public void Create_ShouldAllowNullUserId()
        {
            var result = AuditLog.Create(Guid.NewGuid(), null, "Match", Guid.NewGuid(), "Update", null, null);
            result.IsSuccess.Should().BeTrue();
            result.Value.UserId.Should().BeNull();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = AuditLog.Create(Guid.Empty, null, "Match", Guid.NewGuid(), "Update", null, null);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenEntityTypeIsEmpty()
        {
            var result = AuditLog.Create(Guid.NewGuid(), null, "", Guid.NewGuid(), "Update", null, null);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.EntityType");
        }

        [Fact]
        public void Create_ShouldFail_WhenEntityIdIsEmpty()
        {
            var result = AuditLog.Create(Guid.NewGuid(), null, "Match", Guid.Empty, "Update", null, null);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.EntityId");
        }

        [Fact]
        public void Create_ShouldFail_WhenActionIsEmpty()
        {
            var result = AuditLog.Create(Guid.NewGuid(), null, "Match", Guid.NewGuid(), "", null, null);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Action");
        }
    }
}
