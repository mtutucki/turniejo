using FluentAssertions;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.Players.Domain.Events;

namespace Turniejo.Players.UnitTests.Entities
{
    public sealed class TournamentPlayerTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidTournamentTeamId => Guid.NewGuid();
        private static Guid ValidPlayerId => Guid.NewGuid();
        private static Guid ValidAssignedByUserId => Guid.NewGuid();

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var tournamentTeamId = ValidTournamentTeamId;
            var playerId = ValidPlayerId;

            // Act
            var result = TournamentPlayer.Create(ValidId, tournamentTeamId, playerId, 9, PlayerPosition.Forward, ValidAssignedByUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentTeamId.Should().Be(tournamentTeamId);
            result.Value.PlayerId.Should().Be(playerId);
            result.Value.ShirtNumber.Should().Be(9);
            result.Value.Position.Should().Be(PlayerPosition.Forward);
            result.Value.Status.Should().Be(TournamentPlayerStatus.Active);
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = TournamentPlayer.Create(Guid.Empty, ValidTournamentTeamId, ValidPlayerId, null, null, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentTeamIdIsEmpty()
        {
            var result = TournamentPlayer.Create(ValidId, Guid.Empty, ValidPlayerId, null, null, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentTeamId");
        }

        [Fact]
        public void Create_ShouldFail_WhenPlayerIdIsEmpty()
        {
            var result = TournamentPlayer.Create(ValidId, ValidTournamentTeamId, Guid.Empty, null, null, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
        }

        [Fact]
        public void Create_ShouldFail_WhenShirtNumberIsNegative()
        {
            var result = TournamentPlayer.Create(ValidId, ValidTournamentTeamId, ValidPlayerId, -1, null, ValidAssignedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ShirtNumber");
        }

        [Fact]
        public void Create_ShouldAddPlayerAssignedToTeamEvent()
        {
            var result = TournamentPlayer.Create(ValidId, ValidTournamentTeamId, ValidPlayerId, null, null, ValidAssignedByUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is PlayerAssignedToTeam);
        }

        [Fact]
        public void UpdateDetails_ShouldUpdateFields_WhenValid()
        {
            // Arrange
            var tournamentPlayer = TournamentPlayer.Create(ValidId, ValidTournamentTeamId, ValidPlayerId, 9, PlayerPosition.Forward, ValidAssignedByUserId).Value;
            tournamentPlayer.ClearDomainEvents();

            // Act
            var result = tournamentPlayer.UpdateDetails(10, PlayerPosition.Midfielder, TournamentPlayerStatus.Injured);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentPlayer.ShirtNumber.Should().Be(10);
            tournamentPlayer.Position.Should().Be(PlayerPosition.Midfielder);
            tournamentPlayer.Status.Should().Be(TournamentPlayerStatus.Injured);
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenShirtNumberIsNegative()
        {
            // Arrange
            var tournamentPlayer = TournamentPlayer.Create(ValidId, ValidTournamentTeamId, ValidPlayerId, null, null, ValidAssignedByUserId).Value;

            // Act
            var result = tournamentPlayer.UpdateDetails(-5, null, TournamentPlayerStatus.Active);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ShirtNumber");
        }

        [Fact]
        public void UpdateDetails_ShouldAddTournamentPlayerUpdatedEvent()
        {
            // Arrange
            var tournamentPlayer = TournamentPlayer.Create(ValidId, ValidTournamentTeamId, ValidPlayerId, null, null, ValidAssignedByUserId).Value;
            tournamentPlayer.ClearDomainEvents();

            // Act
            var result = tournamentPlayer.UpdateDetails(7, null, TournamentPlayerStatus.Active);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentPlayer.DomainEvents.Should().ContainSingle(e => e is TournamentPlayerUpdated);
        }
    }
}
