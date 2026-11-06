using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TournamentPlayers.AssignPlayerToTournamentTeam;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class AssignPlayerToTournamentTeamCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AssignPlayerToTournamentTeamCommandHandler _handler;

        public AssignPlayerToTournamentTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var squadEditGuard = new SquadEditGuard(_authorizationService, _tournamentModule);
            _handler = new AssignPlayerToTournamentTeamCommandHandler(_dbContext, _teamModule, _tournamentModule, squadEditGuard, new TeamRosterSync(_dbContext), _currentUserContext);

            // domyślnie: organizer -> blokada składu nie dotyczy (bez tego część istniejących testów musiałaby mockować ustawienia turnieju)
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        private static Player CreateFakePlayer(DateTime birthDate)
        {
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", birthDate, PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            player.ClearDomainEvents();
            return player;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var player = CreateFakePlayer(new DateTime(2012, 1, 1));

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), categoryId, null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _tournamentModule.GetTournamentCategoryAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(categoryId, tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));
            _dbContext.TournamentPlayerExistsAsync(tournamentTeamId, player.Id, Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, player.Id, 9, PlayerPosition.Forward);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
            var command = new AssignPlayerToTournamentTeamCommand(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamBelongsToDifferentTournament()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            var command = new AssignPlayerToTournamentTeamCommand(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);
            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, playerId, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerIsTooOldForCategory()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var player = CreateFakePlayer(new DateTime(2005, 1, 1));

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), categoryId, null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _tournamentModule.GetTournamentCategoryAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(categoryId, tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));

            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, player.Id, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerIsTooYoungForCategory()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var player = CreateFakePlayer(new DateTime(2018, 1, 1));

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), categoryId, null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _tournamentModule.GetTournamentCategoryAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(categoryId, tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));

            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, player.Id, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerAlreadyAssigned()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var player = CreateFakePlayer(new DateTime(2012, 1, 1));

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), categoryId, null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _tournamentModule.GetTournamentCategoryAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns((TournamentCategoryDto?)null);
            _dbContext.TournamentPlayerExistsAsync(tournamentTeamId, player.Id, Arg.Any<CancellationToken>()).Returns(true);

            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, player.Id, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCoachTriesToEditAfterTournamentStarted_AndCanCoachEditSquadAfterStartIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var player = CreateFakePlayer(new DateTime(2012, 1, 1));

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), categoryId, null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);

            // Nie-organizator (np. trener) - blokada składu obowiązuje.
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 16));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));

            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, player.Id, null, null);

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Turniejo.SharedKernel.Exceptions.UnauthorizedException>();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShirtNumberIsTaken()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var player = CreateFakePlayer(new DateTime(2012, 1, 1));

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), categoryId, null, "Accepted"));
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _tournamentModule.GetTournamentCategoryAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns((TournamentCategoryDto?)null);
            _dbContext.TournamentPlayerExistsAsync(tournamentTeamId, player.Id, Arg.Any<CancellationToken>()).Returns(false);
            _dbContext.ShirtNumberExistsAsync(tournamentTeamId, 9, Arg.Any<CancellationToken>()).Returns(true);

            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, player.Id, 9, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ShirtNumber");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
