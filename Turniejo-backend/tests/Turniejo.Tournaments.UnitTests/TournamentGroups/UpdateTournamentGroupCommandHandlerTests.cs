using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentGroups.UpdateTournamentGroup;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentGroups
{
    public sealed class UpdateTournamentGroupCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly UpdateTournamentGroupCommandHandler _handler;

        public UpdateTournamentGroupCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _handler = new UpdateTournamentGroupCommandHandler(_dbContext);
        }

        private static TournamentGroup CreateFakeTournamentGroup()
        {
            var group = TournamentGroup.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Grupa A", 1).Value;

            group.ClearDomainEvents();
            return group;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            var command = new UpdateTournamentGroupCommand(group.TournamentId, group.Id, "Grupa B", 2);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentGroupId.Should().Be(group.Id);
            group.Name.Should().Be("Grupa B");
            group.Order.Should().Be(2);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupNotFound()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentGroupByIdAsync(groupId, Arg.Any<CancellationToken>())
                .Returns((TournamentGroup?)null);
            var command = new UpdateTournamentGroupCommand(Guid.NewGuid(), groupId, "Grupa", 1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentGroup.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupBelongsToAnotherTournament()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            var command = new UpdateTournamentGroupCommand(Guid.NewGuid(), group.Id, "Grupa B", 1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentGroup.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNewNameAlreadyExistsInCategory()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            _dbContext.GroupNameExistsAsync(group.CategoryId, "Grupa B", Arg.Any<CancellationToken>()).Returns(true);
            var command = new UpdateTournamentGroupCommand(group.TournamentId, group.Id, "Grupa B", 1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            group.Name.Should().Be("Grupa A");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenOnlyOrderChanges()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            var command = new UpdateTournamentGroupCommand(group.TournamentId, group.Id, group.Name, 5);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            group.Order.Should().Be(5);
            await _dbContext.DidNotReceive().GroupNameExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
