using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TournamentPlayers.RemovePlayerFromTournamentTeam;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class RemovePlayerFromTournamentTeamCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IMatchModule _matchModule;
        private readonly RemovePlayerFromTournamentTeamCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _tournamentTeamId = Guid.NewGuid();
        private readonly Player _player;
        private readonly TournamentPlayer _tournamentPlayer;

        public RemovePlayerFromTournamentTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _matchModule = Substitute.For<IMatchModule>();
            var authorizationService = Substitute.For<IAuthorizationService>();
            var currentUserContext = Substitute.For<ICurrentUserContext>();
            var squadEditGuard = new SquadEditGuard(authorizationService, Substitute.For<ITournamentModule>());
            _handler = new RemovePlayerFromTournamentTeamCommandHandler(_dbContext, _teamModule, _matchModule, squadEditGuard, currentUserContext);

            // organizator: blokada składu go nie dotyczy
            authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
            currentUserContext.UserId.Returns(Guid.NewGuid());

            _player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 5, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), _tournamentTeamId, _player.Id, 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _tournamentPlayer.ClearDomainEvents();

            _dbContext.GetTournamentPlayerByIdAsync(_tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(_tournamentPlayer);
            _dbContext.GetPlayerByIdAsync(_player.Id, Arg.Any<CancellationToken>()).Returns(_player);
            _dbContext.GetTournamentPlayersByPlayerIdAsync(_player.Id, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { _tournamentPlayer });
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(_player.Id, Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());
            _teamModule.GetTournamentTeamAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(_tournamentTeamId, _tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _matchModule.GetMatchEventsByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TeamMatchEventDto>());
        }

        private RemovePlayerFromTournamentTeamCommand Command() => new(_tournamentId, _tournamentTeamId, _tournamentPlayer.Id);

        [Fact]
        public async Task Handle_ShouldDeleteEntryAndOrphanProfile_WhenPlayerHasNoMatchEvents()
        {
            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Deleted.Should().BeTrue();
            await _dbContext.Received(1).RemoveTournamentPlayerAsync(_tournamentPlayer, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemovePlayerAsync(_player, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldKeepProfile_WhenPlayerIsInAnotherSquad()
        {
            var otherEntry = TournamentPlayer.Create(Guid.NewGuid(), Guid.NewGuid(), _player.Id, 7, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(_player.Id, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { _tournamentPlayer, otherEntry });

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemoveTournamentPlayerAsync(_tournamentPlayer, Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().RemovePlayerAsync(Arg.Any<Player>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldMarkRemovedAndFreeShirtNumber_WhenPlayerHasMatchEvents()
        {
            // Gol w meczu: wpis zostaje (statystyki), ale ze statusem Removed i bez numeru.
            _matchModule.GetMatchEventsByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TeamMatchEventDto> { new(Guid.NewGuid(), "Goal", _player.Id, null) });

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Deleted.Should().BeFalse();
            _tournamentPlayer.Status.Should().Be(TournamentPlayerStatus.Removed);
            _tournamentPlayer.ShirtNumber.Should().BeNull();
            await _dbContext.DidNotReceive().RemoveTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamBelongsToAnotherTournament()
        {
            var command = Command() with { TournamentId = Guid.NewGuid() };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
