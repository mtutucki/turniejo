using FluentAssertions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Teams.UnitTests.Entities
{
    public sealed class TeamStaffTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidTournamentTeamId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();
        private static Guid ValidAssignedByUserId => Guid.NewGuid();

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var tournamentTeamId = ValidTournamentTeamId;
            var userId = ValidUserId;

            // Act
            var result = TeamStaff.Create(ValidId, tournamentTeamId, userId, TeamStaffRole.Manager, ValidAssignedByUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentTeamId.Should().Be(tournamentTeamId);
            result.Value.UserId.Should().Be(userId);
            result.Value.Role.Should().Be(TeamStaffRole.Manager);
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = TeamStaff.Create(Guid.Empty, ValidTournamentTeamId, ValidUserId, TeamStaffRole.Manager, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentTeamIdIsEmpty()
        {
            var result = TeamStaff.Create(ValidId, Guid.Empty, ValidUserId, TeamStaffRole.Manager, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentTeamId");
        }

        [Fact]
        public void Create_ShouldFail_WhenUserIdIsEmpty()
        {
            var result = TeamStaff.Create(ValidId, ValidTournamentTeamId, Guid.Empty, TeamStaffRole.Manager, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.UserId");
        }

        [Fact]
        public void Create_ShouldAddUserAssignedToTeamEvent()
        {
            var result = TeamStaff.Create(ValidId, ValidTournamentTeamId, ValidUserId, TeamStaffRole.AssistantCoach, ValidAssignedByUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is UserAssignedToTeam);
        }

        [Fact]
        public void CreateCoach_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentTeamId = ValidTournamentTeamId;
            var userId = ValidUserId;

            // Act
            var result = TeamStaff.CreateCoach(ValidId, tournamentTeamId, userId, ValidAssignedByUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Role.Should().Be(TeamStaffRole.Coach);
            result.Value.TournamentTeamId.Should().Be(tournamentTeamId);
            result.Value.UserId.Should().Be(userId);
        }

        [Fact]
        public void CreateCoach_ShouldAddCoachAssignedToTeamEvent()
        {
            var result = TeamStaff.CreateCoach(ValidId, ValidTournamentTeamId, ValidUserId, ValidAssignedByUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is CoachAssignedToTeam);
        }

        [Fact]
        public void CreateCoach_ShouldFail_WhenUserIdIsEmpty()
        {
            var result = TeamStaff.CreateCoach(ValidId, ValidTournamentTeamId, Guid.Empty, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.UserId");
        }
    }
}
