using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.GetPlayerMatchHistory;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class GetPlayerMatchHistoryQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetPlayerMatchHistoryQueryHandler _handler;

        public GetPlayerMatchHistoryQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();

            var resolver = new MyChildMatchResolver(_dbContext, _matchModule, _teamModule, _tournamentModule);
            var playerAccessGuard = new PlayerAccessGuard(_dbContext, _teamModule, _authorizationService);
            _handler = new GetPlayerMatchHistoryQueryHandler(_dbContext, resolver, playerAccessGuard, _currentUserContext);

            _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
            _tournamentModule.GetTournamentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentDto?)null);
        }

        [Fact]
        public async Task Handle_ShouldReturnOnlyFinishedMatches_WhenCallerIsThePlayerThemselves()
        {
            // Zamyka lukę z audytu: brakujący GET /api/players/{id}/matches — ogólny odpowiednik
            // GetMyChildResultsQuery, ale dostępny też dla samego zawodnika (nie tylko opiekuna).
            var userId = Guid.NewGuid();
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, userId, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _currentUserContext.UserId.Returns(userId);

            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([tournamentPlayer]);

            var matches = new List<TournamentMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Scheduled", DateTimeOffset.UtcNow.AddDays(1), tournamentTeamId, Guid.NewGuid(), 0, 0),
                new(Guid.NewGuid(), Guid.NewGuid(), "Finished", DateTimeOffset.UtcNow.AddDays(-1), tournamentTeamId, Guid.NewGuid(), 3, 2),
            };
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(matches);

            // Act
            var result = await _handler.Handle(new GetPlayerMatchHistoryQuery(player.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].HomeScore.Should().Be(3);
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCallerHasNoRelationToPlayer()
        {
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([]);
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([]);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            var act = () => _handler.Handle(new GetPlayerMatchHistoryQuery(player.Id), CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
