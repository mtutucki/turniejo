using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TournamentPlayers.ImportSquad;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class ImportSquadCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ImportSquadCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _tournamentTeamId = Guid.NewGuid();
        private readonly Guid _sourceTournamentTeamId = Guid.NewGuid();
        private readonly Guid _teamId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();

        private readonly Player _jan;
        private readonly Player _piotr;

        public ImportSquadCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var squadEditGuard = new SquadEditGuard(_authorizationService, _tournamentModule);
            _handler = new ImportSquadCommandHandler(_dbContext, _teamModule, _tournamentModule, squadEditGuard, _currentUserContext);

            // domyślnie: organizer -> blokada składu nie dotyczy
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            _teamModule.GetTournamentTeamAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(_tournamentTeamId, _tournamentId, _teamId, _categoryId, null, "Accepted"));
            _teamModule.GetTournamentTeamAsync(_sourceTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(_sourceTournamentTeamId, Guid.NewGuid(), _teamId, Guid.NewGuid(), null, "Accepted"));
            _tournamentModule.GetTournamentCategoryAsync(_categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(_categoryId, _tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));

            _jan = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 5, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _piotr = Player.Create(Guid.NewGuid(), "Piotr", "Nowak", new DateTime(2011, 8, 1), PlayerPosition.Defender, null, null, Guid.NewGuid()).Value;

            SourceSquad(SquadEntry(_sourceTournamentTeamId, _jan.Id, 9), SquadEntry(_sourceTournamentTeamId, _piotr.Id, 4));
            CurrentSquad();
            Profiles(_jan, _piotr);
        }

        private static TournamentPlayer SquadEntry(Guid tournamentTeamId, Guid playerId, int? shirtNumber) =>
            TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, shirtNumber, null, Guid.NewGuid()).Value;

        private void SourceSquad(params TournamentPlayer[] entries) =>
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_sourceTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(entries.ToList());

        private void CurrentSquad(params TournamentPlayer[] entries) =>
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(entries.ToList());

        private void Profiles(params Player[] players) =>
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(call => players.Where(p => call.Arg<IReadOnlyCollection<Guid>>().Contains(p.Id)).ToList());

        private ImportSquadCommand Command(params ImportSquadPlayer[] players) =>
            new(_tournamentId, _tournamentTeamId, _sourceTournamentTeamId,
                players.Length > 0
                    ? players
                    : [new ImportSquadPlayer(_jan.Id, 9, PlayerPosition.Forward), new ImportSquadPlayer(_piotr.Id, 4, null)]);

        private async Task ShouldNotPersistAnything()
        {
            await _dbContext.DidNotReceive().AddTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCopyAllSelectedPlayersInOneSave_WhenDataIsValid()
        {
            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentPlayerIds.Should().HaveCount(2);
            await _dbContext.Received(1).AddTournamentPlayerAsync(
                Arg.Is<TournamentPlayer>(tp => tp.TournamentTeamId == _tournamentTeamId && tp.PlayerId == _jan.Id && tp.ShirtNumber == 9 && tp.Position == PlayerPosition.Forward),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddTournamentPlayerAsync(
                Arg.Is<TournamentPlayer>(tp => tp.TournamentTeamId == _tournamentTeamId && tp.PlayerId == _piotr.Id && tp.ShirtNumber == 4),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(2).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamBelongsToDifferentTournament()
        {
            var command = Command() with { TournamentId = Guid.NewGuid() };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenSourceBelongsToDifferentTeam()
        {
            _teamModule.GetTournamentTeamAsync(_sourceTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(_sourceTournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerIsNotInSourceSquad()
        {
            var result = await _handler.Handle(Command(new ImportSquadPlayer(Guid.NewGuid(), null, null)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Player is not part of the source squad");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFailWithoutCopyingAnyone_WhenOnePlayerIsOutsideCategory()
        {
            var tooOld = Player.Create(Guid.NewGuid(), "Adam", "Stary", new DateTime(2010, 12, 31), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            SourceSquad(SquadEntry(_sourceTournamentTeamId, _jan.Id, 9), SquadEntry(_sourceTournamentTeamId, tooOld.Id, 7));
            Profiles(_jan, tooOld);

            var result = await _handler.Handle(
                Command(new ImportSquadPlayer(_jan.Id, 9, null), new ImportSquadPlayer(tooOld.Id, 7, null)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Player is too old for this category");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerAlreadyInTargetSquad()
        {
            CurrentSquad(SquadEntry(_tournamentTeamId, _jan.Id, null));

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Player is already assigned to this team");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShirtNumberTakenInTargetSquad()
        {
            CurrentSquad(SquadEntry(_tournamentTeamId, Guid.NewGuid(), 9));

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Shirt number is already taken in this team");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShirtNumberRepeatsWithinRequest()
        {
            var result = await _handler.Handle(
                Command(new ImportSquadPlayer(_jan.Id, 9, null), new ImportSquadPlayer(_piotr.Id, 9, null)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Shirt number is already taken in this team");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCoachEditsAfterStartAndSettingDisallows()
        {
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(_tournamentId, "Cup", "InProgress", null, Guid.NewGuid(), 16));
            _tournamentModule.GetTournamentSettingsAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(_tournamentId, 20, 5, 10, 3, 1, 0, false, false, 3, 0));

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
            await ShouldNotPersistAnything();
        }
    }
}
