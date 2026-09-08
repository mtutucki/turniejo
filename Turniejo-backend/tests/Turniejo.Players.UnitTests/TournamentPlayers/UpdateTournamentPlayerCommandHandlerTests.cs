using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class UpdateTournamentPlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ITournamentModule _tournamentModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly UpdateTournamentPlayerCommandHandler _handler;

        public UpdateTournamentPlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var squadEditGuard = new SquadEditGuard(_authorizationService, _tournamentModule);
            _handler = new UpdateTournamentPlayerCommandHandler(_dbContext, _teamModule, squadEditGuard, _currentUserContext);

            // domyślnie: organizer -> blokada składu nie dotyczy
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
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
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _dbContext.ShirtNumberExistsAsync(tournamentTeamId, 10, Arg.Any<CancellationToken>()).Returns(false);
            var command = new UpdateTournamentPlayerCommand(tournamentId, tournamentTeamId, tournamentPlayer.Id, 10, PlayerPosition.Midfielder, TournamentPlayerStatus.Active);

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
        public async Task Handle_ShouldFail_WhenTournamentTeamDoesNotExist()
        {
            // Arrange — sam TournamentPlayer wskazuje na drużynę, ale ITeamModule jej nie zna
            // (niespójność danych) — ma się zakończyć czytelnym NotFound, nie NRE ani fail-open.
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
            var command = new UpdateTournamentPlayerCommand(Guid.NewGuid(), tournamentTeamId, tournamentPlayer.Id, null, null, TournamentPlayerStatus.Active);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNewShirtNumberIsTaken()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _dbContext.ShirtNumberExistsAsync(tournamentTeamId, 11, Arg.Any<CancellationToken>()).Returns(true);
            var command = new UpdateTournamentPlayerCommand(tournamentId, tournamentTeamId, tournamentPlayer.Id, 11, null, TournamentPlayerStatus.Active);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ShirtNumber");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCoachTriesToEditAfterTournamentStarted_AndCanCoachEditSquadAfterStartIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));

            // Nie-organizator (np. trener) - blokada składu obowiązuje.
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 16));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));

            var command = new UpdateTournamentPlayerCommand(tournamentId, tournamentTeamId, tournamentPlayer.Id, null, null, TournamentPlayerStatus.Active);

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCoachSendsSpoofedTournamentId_EvenIfItResolvesToNothing()
        {
            // Regresja na dokładnie tę lukę z audytu: trener (AlternateScope, nie Organizer) podstawia
            // w body losowy/nieistniejący TournamentId, licząc na to, że handler go użyje wprost do
            // sprawdzenia blokady. Prawdziwy TournamentId ma być wzięty z relacji TournamentTeamId
            // (przez ITeamModule), więc spoofowana wartość w ogóle nie trafia do SquadEditGuard —
            // a i tak zostałaby zablokowana (fail-closed), gdyby trafiła.
            var realTournamentId = Guid.NewGuid();
            var spoofedTournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = CreateFakeTournamentPlayer(tournamentTeamId);
            _dbContext.GetTournamentPlayerByIdAsync(tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(tournamentPlayer);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, realTournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));

            // Trener: nie-organizator w scope realTournamentId (jedyny, jaki handler powinien sprawdzić).
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(realTournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(realTournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 16));
            _tournamentModule.GetTournamentSettingsAsync(realTournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(realTournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
            // Spoofowany TournamentId celowo nie ma żadnego ustawienia zamockowanego — gdyby handler
            // wciąż go używał, SquadEditGuard dostałby null/null i (po naprawie fail-open) też rzuciłby,
            // ale test dowodzi że handler w ogóle go nie pyta: weryfikujemy to niżej.

            var command = new UpdateTournamentPlayerCommand(spoofedTournamentId, tournamentTeamId, tournamentPlayer.Id, null, null, TournamentPlayerStatus.Active);

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
            await _tournamentModule.DidNotReceive().GetTournamentAsync(spoofedTournamentId, Arg.Any<CancellationToken>());
            await _tournamentModule.DidNotReceive().GetTournamentSettingsAsync(spoofedTournamentId, Arg.Any<CancellationToken>());
        }
    }
}
