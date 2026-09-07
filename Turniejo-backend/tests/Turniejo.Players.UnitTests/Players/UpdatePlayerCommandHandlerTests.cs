using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.UpdatePlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class UpdatePlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly UpdatePlayerCommandHandler _handler;

        public UpdatePlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _handler = new UpdatePlayerCommandHandler(_dbContext);
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
    }
}
