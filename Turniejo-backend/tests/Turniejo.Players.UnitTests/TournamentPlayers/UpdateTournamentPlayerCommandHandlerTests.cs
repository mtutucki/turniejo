using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class UpdateTournamentPlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly UpdateTournamentPlayerCommandHandler _handler;

        public UpdateTournamentPlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _handler = new UpdateTournamentPlayerCommandHandler(_dbContext);
        }

        private static TournamentPlayer CreateFakeTournamentPlayer(Guid tournamentTeamId)
        {
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            tournamentPlayer.ClearDomainEvents();
            return tournamentPlayer;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _dbContext.ShirtNumberExistsAsync(tournamentTeamId, 10, Arg.Any<CancellationToken>()).Returns(false);
            var command = new UpdateTournamentPlayerCommand(Guid.NewGuid(), tournamentTeamId, tournamentPlayer.Id, 10, PlayerPosition.Midfielder, TournamentPlayerStatus.Active);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentPlayer.ShirtNumber.Should().Be(10);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentPlayerNotFound()
        {
            // Arrange
            var tournamentPlayerId = Guid.NewGuid();
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayerId, Arg.Any<CancellationToken>()).Returns((TournamentPlayer?)null);
            var command = new UpdateTournamentPlayerCommand(Guid.NewGuid(), Guid.NewGuid(), tournamentPlayerId, null, null, TournamentPlayerStatus.Active);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentPlayerBelongsToDifferentTeam()
        {
            // Arrange
            var tournamentPlayer = CreateFakeTournamentPlayer(Guid.NewGuid());
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            var command = new UpdateTournamentPlayerCommand(Guid.NewGuid(), Guid.NewGuid(), tournamentPlayer.Id, null, null, TournamentPlayerStatus.Active);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNewShirtNumberIsTaken()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _dbContext.ShirtNumberExistsAsync(tournamentTeamId, 11, Arg.Any<CancellationToken>()).Returns(true);
            var command = new UpdateTournamentPlayerCommand(Guid.NewGuid(), tournamentTeamId, tournamentPlayer.Id, 11, null, TournamentPlayerStatus.Active);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ShirtNumber");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
