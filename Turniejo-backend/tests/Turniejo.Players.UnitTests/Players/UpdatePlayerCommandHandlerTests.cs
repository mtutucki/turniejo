using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.UpdatePlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class UpdatePlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly UpdatePlayerCommandHandler _handler;

        public UpdatePlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new UpdatePlayerCommandHandler(_dbContext, _currentUserContext);

            // domyślnie: brak opiekunów -> edycja otwarta (bez tego mock zwróciłby null i wywalił NRE)
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());
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
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
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
    }
}
