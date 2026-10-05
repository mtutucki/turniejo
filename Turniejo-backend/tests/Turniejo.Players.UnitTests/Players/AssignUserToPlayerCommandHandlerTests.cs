using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.AssignUserToPlayer;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class AssignUserToPlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AssignUserToPlayerCommandHandler _handler;

        public AssignUserToPlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());
            _dbContext.GetTournamentPlayersByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);

            var playerAccessGuard = new PlayerAccessGuard(_dbContext, _teamModule, _authorizationService);
            _handler = new AssignUserToPlayerCommandHandler(_dbContext, playerAccessGuard, _currentUserContext);
        }

        private static Player CreateFakePlayer()
        {
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            player.ClearDomainEvents();
            return player;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenCallerIsGuardianWithCanEdit()
        {
            // Arrange
            var player = CreateFakePlayer();
            var callerId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, callerId, RelationshipType.Parent, canEdit: true, canReceiveNotifications: true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
            _currentUserContext.UserId.Returns(callerId);
            var command = new AssignUserToPlayerCommand(player.Id, userId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            player.UserId.Should().Be(userId);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerNotFound()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);
            var command = new AssignUserToPlayerCommand(playerId, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCallerHasNoRelationToPlayer()
        {
            // Zamyka IDOR z audytu: bez tej poprawki dowolny zalogowany user mógł podpiąć
            // dowolne konto (także własne) do cudzego profilu zawodnika.
            var player = CreateFakePlayer();
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AssignUserToPlayerCommand(player.Id, Guid.NewGuid());

            // Act
            var act = () => _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
