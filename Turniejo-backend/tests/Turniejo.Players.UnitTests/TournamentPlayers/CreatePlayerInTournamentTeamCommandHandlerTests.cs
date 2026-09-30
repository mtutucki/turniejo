using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TournamentPlayers.CreatePlayerInTournamentTeam;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class CreatePlayerInTournamentTeamCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly CreatePlayerInTournamentTeamCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _tournamentTeamId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();

        public CreatePlayerInTournamentTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var squadEditGuard = new SquadEditGuard(_authorizationService, _tournamentModule);
            _handler = new CreatePlayerInTournamentTeamCommandHandler(_dbContext, _teamModule, _tournamentModule, squadEditGuard, _currentUserContext);

            // domyślnie: organizer -> blokada składu nie dotyczy
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            _teamModule.GetTournamentTeamAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(_tournamentTeamId, _tournamentId, Guid.NewGuid(), _categoryId, null, "Accepted"));
            _tournamentModule.GetTournamentCategoryAsync(_categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(_categoryId, _tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer>());
        }

        private CreatePlayerInTournamentTeamCommand Command(DateTime? birthDate = null, int? shirtNumber = 9) =>
            new(_tournamentId, _tournamentTeamId, "Jan", "Kowalski", birthDate ?? new DateTime(2012, 5, 1),
                PlayerPosition.Forward, null, shirtNumber, PlayerPosition.Forward);

        private async Task ShouldNotPersistAnything()
        {
            await _dbContext.DidNotReceive().AddPlayerAsync(Arg.Any<Player>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().AddTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCreateProfileAndSquadEntryInOneSave_WhenDataIsValid()
        {
            // Act
            var result = await _handler.Handle(Command(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddPlayerAsync(
                Arg.Is<Player>(p => p.Id == result.Value.PlayerId && p.FirstName == "Jan"), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddTournamentPlayerAsync(
                Arg.Is<TournamentPlayer>(tp => tp.Id == result.Value.TournamentPlayerId && tp.PlayerId == result.Value.PlayerId && tp.ShirtNumber == 9),
                Arg.Any<CancellationToken>());
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
        public async Task Handle_ShouldFailWithoutCreatingProfile_WhenPlayerTooOld()
        {
            var result = await _handler.Handle(Command(new DateTime(2010, 12, 31)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Player is too old for this category");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFailWithoutCreatingProfile_WhenPlayerTooYoung()
        {
            var result = await _handler.Handle(Command(new DateTime(2013, 1, 2)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Player is too young for this category");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFailWithoutCreatingProfile_WhenShirtNumberTaken()
        {
            _dbContext.ShirtNumberExistsAsync(_tournamentTeamId, 9, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Shirt number is already taken in this team");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldFailWithoutCreatingProfile_WhenSamePersonAlreadyInSquad()
        {
            var existing = Player.Create(Guid.NewGuid(), " jan ", "KOWALSKI", new DateTime(2012, 5, 1), PlayerPosition.Defender, null, null, Guid.NewGuid()).Value;
            var squadEntry = TournamentPlayer.Create(Guid.NewGuid(), _tournamentTeamId, existing.Id, 5, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { squadEntry });
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Player> { existing });

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Player is already assigned to this team");
            await ShouldNotPersistAnything();
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenNamesakeHasDifferentBirthDate()
        {
            var existing = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2011, 3, 3), PlayerPosition.Defender, null, null, Guid.NewGuid()).Value;
            var squadEntry = TournamentPlayer.Create(Guid.NewGuid(), _tournamentTeamId, existing.Id, 5, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { squadEntry });
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Player> { existing });

            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }
    }
}
