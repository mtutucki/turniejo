using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.PlayerGuardians.AssignGuardianToPlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.UnitTests.PlayerGuardians
{
    public sealed class AssignGuardianToPlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AssignGuardianToPlayerCommandHandler _handler;

        public AssignGuardianToPlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new AssignGuardianToPlayerCommandHandler(_dbContext, _userModule, _currentUserContext);
        }

        private static Player CreateFakePlayer()
        {
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            player.ClearDomainEvents();
            return player;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var player = CreateFakePlayer();
            var userId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.PlayerGuardianExistsAsync(player.Id, userId, Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AssignGuardianToPlayerCommand(player.Id, userId, RelationshipType.Parent, true, true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddPlayerGuardianAsync(Arg.Any<PlayerGuardian>(), Arg.Any<CancellationToken>());
            await _userModule.Received(1).AssignRoleAsync(
                userId, nameof(UserRole.Parent), nameof(RoleScopeType.Player), player.Id, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerNotFound()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);
            var command = new AssignGuardianToPlayerCommand(playerId, Guid.NewGuid(), RelationshipType.Parent, true, true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenUserIsAlreadyGuardian()
        {
            // Arrange
            var player = CreateFakePlayer();
            var userId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.PlayerGuardianExistsAsync(player.Id, userId, Arg.Any<CancellationToken>()).Returns(true);
            var command = new AssignGuardianToPlayerCommand(player.Id, userId, RelationshipType.Parent, true, true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.UserId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
