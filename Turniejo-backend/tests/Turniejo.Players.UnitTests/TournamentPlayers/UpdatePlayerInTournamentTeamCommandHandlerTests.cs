using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TournamentPlayers.UpdatePlayerInTournamentTeam;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class UpdatePlayerInTournamentTeamCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly UpdatePlayerInTournamentTeamCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _tournamentTeamId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();
        private readonly Player _player;
        private readonly TournamentPlayer _tournamentPlayer;

        public UpdatePlayerInTournamentTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var squadEditGuard = new SquadEditGuard(_authorizationService, _tournamentModule);
            _handler = new UpdatePlayerInTournamentTeamCommandHandler(_dbContext, _teamModule, _tournamentModule, squadEditGuard, _currentUserContext);

            // domyślnie: organizer -> blokada składu nie dotyczy
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            _player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 5, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _player.ClearDomainEvents();
            _tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), _tournamentTeamId, _player.Id, 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _tournamentPlayer.ClearDomainEvents();

            _dbContext.GetTournamentPlayerByIdAsync(_tournamentPlayer.Id, Arg.Any<CancellationToken>()).Returns(_tournamentPlayer);
            _dbContext.GetPlayerByIdAsync(_player.Id, Arg.Any<CancellationToken>()).Returns(_player);
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { _tournamentPlayer });
            _teamModule.GetTournamentTeamAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(_tournamentTeamId, _tournamentId, Guid.NewGuid(), _categoryId, null, "Accepted"));
            _tournamentModule.GetTournamentCategoryAsync(_categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(_categoryId, _tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));
        }

        private UpdatePlayerInTournamentTeamCommand Command(
            string firstName = "Janek", DateTime? birthDate = null, int? shirtNumber = 10) =>
            new(_tournamentId, _tournamentTeamId, _tournamentPlayer.Id, firstName, "Kowalski",
                birthDate ?? new DateTime(2012, 5, 1), null, shirtNumber, PlayerPosition.Midfielder, TournamentPlayerStatus.Injured);

        private async Task ShouldNotSave() =>
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        [Fact]
        public async Task Handle_ShouldUpdateProfileAndSquadEntryInOneSave_WhenDataIsValid()
        {
            var result = await _handler.Handle(Command(birthDate: new DateTime(2012, 6, 2)), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _player.FirstName.Should().Be("Janek");
            _player.BirthDate.Should().Be(new DateTime(2012, 6, 2));
            _player.DefaultPosition.Should().Be(PlayerPosition.Forward);
            _tournamentPlayer.ShirtNumber.Should().Be(10);
            _tournamentPlayer.Position.Should().Be(PlayerPosition.Midfielder);
            _tournamentPlayer.Status.Should().Be(TournamentPlayerStatus.Injured);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentPlayerBelongsToDifferentTeam()
        {
            var command = Command() with { TournamentTeamId = Guid.NewGuid() };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentPlayer.NotFound");
            await ShouldNotSave();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamBelongsToDifferentTournament()
        {
            var command = Command() with { TournamentId = Guid.NewGuid() };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
            _player.FirstName.Should().Be("Jan");
            await ShouldNotSave();
        }

        [Fact]
        public async Task Handle_ShouldFailWithoutChangingAnything_WhenNewBirthDateIsOutsideCategory()
        {
            var result = await _handler.Handle(Command(birthDate: new DateTime(2010, 12, 31)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.BirthDate");
            result.Error.Message.Should().Be("Player is too old for this category");
            _player.FirstName.Should().Be("Jan");
            _tournamentPlayer.ShirtNumber.Should().Be(9);
            await ShouldNotSave();
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenBirthDateIsOutsideCategoryButUnchanged()
        {
            _tournamentModule.GetTournamentCategoryAsync(_categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(_categoryId, _tournamentId, "U10", new DateTime(2015, 1, 1), null));

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _tournamentPlayer.ShirtNumber.Should().Be(10);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShirtNumberTakenByAnotherPlayer()
        {
            _dbContext.ShirtNumberExistsAsync(_tournamentTeamId, 10, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Shirt number is already taken in this team");
            _player.FirstName.Should().Be("Jan");
            await ShouldNotSave();
        }

        [Fact]
        public async Task Handle_ShouldNotCheckShirtNumber_WhenItIsUnchanged()
        {
            _dbContext.ShirtNumberExistsAsync(_tournamentTeamId, 9, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _handler.Handle(Command(shirtNumber: 9), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenEditMakesPlayerDuplicateOfSquadMate()
        {
            var mate = Player.Create(Guid.NewGuid(), "Piotr", "Kowalski", new DateTime(2012, 5, 1), PlayerPosition.Defender, null, null, Guid.NewGuid()).Value;
            var mateEntry = TournamentPlayer.Create(Guid.NewGuid(), _tournamentTeamId, mate.Id, 5, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { _tournamentPlayer, mateEntry });
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Player> { mate });

            var result = await _handler.Handle(Command(firstName: "piotr"), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("A player with the same name and birth date is already in this team");
            await ShouldNotSave();
        }

        [Fact]
        public async Task Handle_ShouldSave_WhenDuplicateOfSquadMateIsConfirmed()
        {
            var mate = Player.Create(Guid.NewGuid(), "Piotr", "Kowalski", new DateTime(2012, 5, 1), PlayerPosition.Defender, null, null, Guid.NewGuid()).Value;
            var mateEntry = TournamentPlayer.Create(Guid.NewGuid(), _tournamentTeamId, mate.Id, 5, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { _tournamentPlayer, mateEntry });
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Player> { mate });

            var result = await _handler.Handle(Command(firstName: "piotr") with { AllowDuplicate = true }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
            _player.FirstName.Should().Be("Jan");
            await ShouldNotSave();
        }
    }
}
