using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.TournamentPlayers.GetPublicPlayerProfile;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class GetPublicPlayerProfileQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicPlayerProfileQueryHandler _handler;

        public GetPublicPlayerProfileQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicPlayerProfileQueryHandler(_dbContext, _teamModule, _tournamentModule);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentPlayerNotFound()
        {
            // Arrange
            var id = Guid.NewGuid();
            _dbContext.GetTournamentPlayerByIdAsync(id, Arg.Any<CancellationToken>()).Returns((TournamentPlayer?)null);

            // Act
            var result = await _handler.Handle(new GetPublicPlayerProfileQuery(id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShowPlayersIsFalse()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, false, true, true, true, true));

            // Act
            var result = await _handler.Handle(new GetPublicPlayerProfileQuery(tournamentPlayer.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentIsNotPublic()
        {
            // Odróżnia się od ShowPlayersIsFalse: to prywatny turniej (IsPublic=false), inny realny
            // scenariusz niż "publiczny, ale organizator wyłączył listę zawodników" — dotąd nietestowany.
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true));

            // Act
            var result = await _handler.Handle(new GetPublicPlayerProfileQuery(tournamentPlayer.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Zawodnik osierocony (TournamentTeamId wskazujący na już nieistniejącą/niedostępną
            // drużynę) — nietestowana wcześniej gałąź.
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentPlayer.TournamentTeamId, Arg.Any<CancellationToken>())
                .Returns((TournamentTeamDto?)null);

            // Act
            var result = await _handler.Handle(new GetPublicPlayerProfileQuery(tournamentPlayer.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnProfile_WhenTournamentIsPublicAndShowPlayersIsTrue()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var playerId = Guid.NewGuid();

            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, teamId, Guid.NewGuid(), null, "Accepted"));
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, true));

            var player = Player.Create(playerId, "Jan", "Kowalski", new DateTime(2010, 1, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(player);
            _teamModule.GetTeamAsync(teamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(teamId, "Lions", "LIO"));

            // Act
            var result = await _handler.Handle(new GetPublicPlayerProfileQuery(tournamentPlayer.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.FirstName.Should().Be("Jan");
            result.Value.TeamName.Should().Be("Lions");
            result.Value.ShirtNumber.Should().Be(9);
        }
    }
}
