using FluentAssertions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Teams.UnitTests.Entities
{
    public sealed class TournamentTeamTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidTournamentId => Guid.NewGuid();
        private static Guid ValidTeamId => Guid.NewGuid();
        private static Guid ValidCategoryId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();

        private static TournamentTeam CreateValidTournamentTeam()
        {
            var tournamentTeam = TournamentTeam.Create(ValidId, ValidTournamentId, ValidTeamId, ValidCategoryId, ValidUserId).Value;
            tournamentTeam.ClearDomainEvents();
            return tournamentTeam;
        }

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var tournamentId = ValidTournamentId;
            var teamId = ValidTeamId;
            var categoryId = ValidCategoryId;

            // Act
            var result = TournamentTeam.Create(ValidId, tournamentId, teamId, categoryId, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentId.Should().Be(tournamentId);
            result.Value.TeamId.Should().Be(teamId);
            result.Value.CategoryId.Should().Be(categoryId);
            result.Value.RegistrationStatus.Should().Be(RegistrationStatus.Pending);
            result.Value.PaymentStatus.Should().Be(PaymentStatus.NotRequired);
            result.Value.GroupId.Should().BeNull();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = TournamentTeam.Create(Guid.Empty, ValidTournamentId, ValidTeamId, ValidCategoryId, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentIdIsEmpty()
        {
            var result = TournamentTeam.Create(ValidId, Guid.Empty, ValidTeamId, ValidCategoryId, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
        }

        [Fact]
        public void Create_ShouldFail_WhenTeamIdIsEmpty()
        {
            var result = TournamentTeam.Create(ValidId, ValidTournamentId, Guid.Empty, ValidCategoryId, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TeamId");
        }

        [Fact]
        public void Create_ShouldFail_WhenCategoryIdIsEmpty()
        {
            var result = TournamentTeam.Create(ValidId, ValidTournamentId, ValidTeamId, Guid.Empty, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.CategoryId");
        }

        [Fact]
        public void Create_ShouldAddTeamAddedToTournamentEvent()
        {
            var result = TournamentTeam.Create(ValidId, ValidTournamentId, ValidTeamId, ValidCategoryId, ValidUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is TeamAddedToTournament);
        }

        [Theory]
        [InlineData(RegistrationStatus.Accepted)]
        [InlineData(RegistrationStatus.Rejected)]
        public void ChangeRegistrationStatus_ShouldSucceed_FromPending(RegistrationStatus newStatus)
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();

            // Act
            var result = tournamentTeam.ChangeRegistrationStatus(newStatus, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.RegistrationStatus.Should().Be(newStatus);
        }

        [Fact]
        public void ChangeRegistrationStatus_ShouldSucceed_FromAcceptedToWithdrawn()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();
            tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Accepted, ValidUserId);

            // Act
            var result = tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Withdrawn, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.RegistrationStatus.Should().Be(RegistrationStatus.Withdrawn);
        }

        [Fact]
        public void ChangeRegistrationStatus_ShouldFail_WhenStatusIsSame()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();

            // Act
            var result = tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Pending, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RegistrationStatus");
        }

        [Fact]
        public void ChangeRegistrationStatus_ShouldFail_WhenTransitionIsNotAllowed()
        {
            // Arrange — Rejected to jest stan terminalny
            var tournamentTeam = CreateValidTournamentTeam();
            tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Rejected, ValidUserId);

            // Act
            var result = tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Accepted, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RegistrationStatus");
        }

        [Fact]
        public void ChangeRegistrationStatus_ShouldAddEvent_WhenValid()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();

            // Act
            var result = tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Accepted, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.DomainEvents.Should().ContainSingle(e => e is TeamRegistrationStatusChanged);
        }

        [Fact]
        public void ChangePaymentStatus_ShouldUpdateStatus_WhenValid()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();

            // Act
            var result = tournamentTeam.ChangePaymentStatus(PaymentStatus.Paid, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.PaymentStatus.Should().Be(PaymentStatus.Paid);
        }

        [Fact]
        public void ChangePaymentStatus_ShouldFail_WhenStatusIsSame()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();

            // Act
            var result = tournamentTeam.ChangePaymentStatus(PaymentStatus.NotRequired, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PaymentStatus");
        }

        [Fact]
        public void ChangePaymentStatus_ShouldAddEvent_WhenValid()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();

            // Act
            var result = tournamentTeam.ChangePaymentStatus(PaymentStatus.Paid, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.DomainEvents.Should().ContainSingle(e => e is TeamPaymentStatusChanged);
        }

        [Fact]
        public void AssignToGroup_ShouldSetGroupId()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();
            var groupId = Guid.NewGuid();

            // Act
            var result = tournamentTeam.AssignToGroup(groupId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.GroupId.Should().Be(groupId);
        }

        [Fact]
        public void AssignToGroup_ShouldAllowUnassigning()
        {
            // Arrange
            var tournamentTeam = CreateValidTournamentTeam();
            tournamentTeam.AssignToGroup(Guid.NewGuid());

            // Act
            var result = tournamentTeam.AssignToGroup(null);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.GroupId.Should().BeNull();
        }
    }
}
