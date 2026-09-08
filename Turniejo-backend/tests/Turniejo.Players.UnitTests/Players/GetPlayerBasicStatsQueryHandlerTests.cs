using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.GetPlayerBasicStats;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class GetPlayerBasicStatsQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly ITeamModule _teamModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetPlayerBasicStatsQueryHandler _handler;

        public GetPlayerBasicStatsQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var playerAccessGuard = new PlayerAccessGuard(_dbContext, _teamModule, _authorizationService);
            _handler = new GetPlayerBasicStatsQueryHandler(_dbContext, _matchModule, playerAccessGuard, _currentUserContext);
        }

        private static Player CreatePlayer(Guid userId) =>
            Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, userId, Guid.NewGuid()).Value;

        [Fact]
        public async Task Handle_ShouldAggregateStats_AcrossAllTournamentTeams()
        {
            // Zamyka lukę z audytu: brakujący GET /api/players/{id}/stats. Zawodnik grał w dwóch
            // drużynach (dwa TournamentPlayer) — statystyki muszą się zsumować po obu.
            var userId = Guid.NewGuid();
            var player = CreatePlayer(userId);
            _currentUserContext.UserId.Returns(userId);
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);

            var teamAId = Guid.NewGuid();
            var teamBId = Guid.NewGuid();
            var tpA = TournamentPlayer.Create(Guid.NewGuid(), teamAId, player.Id, null, null, Guid.NewGuid()).Value;
            var tpB = TournamentPlayer.Create(Guid.NewGuid(), teamBId, player.Id, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([tpA, tpB]);

            _matchModule.GetMatchEventsByTournamentTeamIdAsync(teamAId, Arg.Any<CancellationToken>())
                .Returns([new TeamMatchEventDto(Guid.NewGuid(), "Goal", player.Id, null)]);
            _matchModule.GetMatchEventsByTournamentTeamIdAsync(teamBId, Arg.Any<CancellationToken>())
                .Returns([
                    new TeamMatchEventDto(Guid.NewGuid(), "Goal", player.Id, null),
                    new TeamMatchEventDto(Guid.NewGuid(), "YellowCard", player.Id, null)
                ]);

            // Act
            var result = await _handler.Handle(new GetPlayerBasicStatsQuery(player.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Goals.Should().Be(2);
            result.Value.YellowCards.Should().Be(1);
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenPlayerNotExist()
        {
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);

            var result = await _handler.Handle(new GetPlayerBasicStatsQuery(playerId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCallerHasNoRelationToPlayer()
        {
            var player = CreatePlayer(Guid.NewGuid());
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([]);
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([]);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            var act = () => _handler.Handle(new GetPlayerBasicStatsQuery(player.Id), CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
