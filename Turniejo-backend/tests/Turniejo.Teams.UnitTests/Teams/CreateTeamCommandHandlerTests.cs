using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Teams.CreateTeam;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.Teams
{
    public sealed class CreateTeamCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly CreateTeamCommandHandler _handler;

        public CreateTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new CreateTeamCommandHandler(_dbContext, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CreateTeamCommand(Guid.NewGuid(), "FC Testowy", "FCT", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TeamId.Should().NotBeEmpty();
            await _dbContext.Received(1).AddTeamAsync(Arg.Any<Team>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNameIsInvalid()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CreateTeamCommand(Guid.NewGuid(), string.Empty, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CreateTeamCommand(Guid.NewGuid(), "FC Testowy", null, null);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }
    }
}
