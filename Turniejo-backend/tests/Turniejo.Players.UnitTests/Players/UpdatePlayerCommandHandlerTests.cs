using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.UpdatePlayer;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class UpdatePlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly UpdatePlayerCommandHandler _handler;

        public UpdatePlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();

            // domyślnie: brak opiekunów i brak przypisań do drużyn — testy nadpisują to, co im potrzebne
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());
            _dbContext.GetTournamentPlayersByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);

            var playerAccessGuard = new PlayerAccessGuard(_dbContext, _teamModule, _authorizationService);
            _handler = new UpdatePlayerCommandHandler(_dbContext, playerAccessGuard, _currentUserContext);
        }

        private static Player CreateFakePlayer(Guid? userId = null)
        {
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, userId, Guid.NewGuid()).Value;
            player.ClearDomainEvents();
            return player;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenCallerIsLinkedPlayer()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var player = CreateFakePlayer(userId);
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _currentUserContext.UserId.Returns(userId);
            var command = new UpdatePlayerCommand(player.Id, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            player.FirstName.Should().Be("Adam");
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerNotFound()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);
            var command = new UpdatePlayerCommand(playerId, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenCallerIsGuardianWithCanEdit()
        {
            // Arrange
            var player = CreateFakePlayer();
            var userId = Guid.NewGuid();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, userId, RelationshipType.Parent, canEdit: true, canReceiveNotifications: true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
            _currentUserContext.UserId.Returns(userId);
            var command = new UpdatePlayerCommand(player.Id, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCallerIsGuardianWithoutCanEdit()
        {
            // Arrange
            var player = CreateFakePlayer();
            var userId = Guid.NewGuid();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, userId, RelationshipType.Parent, canEdit: false, canReceiveNotifications: true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
            _currentUserContext.UserId.Returns(userId);
            var command = new UpdatePlayerCommand(player.Id, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCallerIsNotAGuardianOfThisPlayer()
        {
            // Arrange
            var player = CreateFakePlayer();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, Guid.NewGuid(), RelationshipType.Parent, canEdit: true, canReceiveNotifications: true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new UpdatePlayerCommand(player.Id, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerHasNoGuardiansAndCallerIsUnrelated()
        {
            // Regresja na fail-open z audytu: brak opiekunów NIE oznacza już otwartej edycji.
            var player = CreateFakePlayer();
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new UpdatePlayerCommand(player.Id, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenCallerIsOrganizerOfPlayersTournament()
        {
            // Arrange
            var player = CreateFakePlayer();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>())
                .Returns([TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value]);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _currentUserContext.UserId.Returns(userId);
            var command = new UpdatePlayerCommand(player.Id, "Adam", "Nowak", new DateTime(2012, 1, 1), PlayerPosition.Goalkeeper, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
    }
}
