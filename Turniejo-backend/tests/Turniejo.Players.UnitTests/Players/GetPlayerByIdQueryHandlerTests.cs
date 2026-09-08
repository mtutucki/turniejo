using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.GetPlayerById;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class GetPlayerByIdQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly GetPlayerByIdQueryHandler _handler;

        public GetPlayerByIdQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _handler = new GetPlayerByIdQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDto_WhenPlayerExists()
        {
            // Arrange
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, "photo.png", null, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);

            // Act
            var result = await _handler.Handle(new GetPlayerByIdQuery(player.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(player.Id);
            result.Value.FirstName.Should().Be(player.FirstName);
            result.Value.DefaultPosition.Should().Be(player.DefaultPosition.ToString());
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenPlayerNotExist()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);

            // Act
            var result = await _handler.Handle(new GetPlayerByIdQuery(playerId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
        }
    }
}
