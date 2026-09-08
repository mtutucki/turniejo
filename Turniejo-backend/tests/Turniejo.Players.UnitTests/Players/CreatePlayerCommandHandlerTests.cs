using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.CreatePlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class CreatePlayerCommandHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly CreatePlayerCommandHandler _handler;

        public CreatePlayerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new CreatePlayerCommandHandler(_dbContext, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CreatePlayerCommand("Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PlayerId.Should().NotBeEmpty();
            await _dbContext.Received(1).AddPlayerAsync(Arg.Any<Player>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenFirstNameIsInvalid()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CreatePlayerCommand(string.Empty, "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.FirstName");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CreatePlayerCommand("Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }
    }
}
