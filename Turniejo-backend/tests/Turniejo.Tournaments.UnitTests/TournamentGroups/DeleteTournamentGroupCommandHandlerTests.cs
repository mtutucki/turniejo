using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentGroups.DeleteTournamentGroup;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentGroups
{
    public sealed class DeleteTournamentGroupCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IMatchModule _matchModule;
        private readonly DeleteTournamentGroupCommandHandler _handler;

        public DeleteTournamentGroupCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _matchModule = Substitute.For<IMatchModule>();
            _handler = new DeleteTournamentGroupCommandHandler(_dbContext, _teamModule, _matchModule);
        }

        private static TournamentGroup CreateFakeTournamentGroup()
        {
            var group = TournamentGroup.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Grupa A", 1).Value;

            group.ClearDomainEvents();
            return group;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenGroupHasNoTeamsOrMatches()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            var command = new DeleteTournamentGroupCommand(group.TournamentId, group.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemoveTournamentGroupAsync(group, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupNotFound()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentGroupByIdAsync(groupId, Arg.Any<CancellationToken>())
                .Returns((TournamentGroup?)null);

            // Act
            var result = await _handler.Handle(new DeleteTournamentGroupCommand(Guid.NewGuid(), groupId), CancellationToken.None);

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

            // Act
            var result = await _handler.Handle(new DeleteTournamentGroupCommand(Guid.NewGuid(), group.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentGroup.NotFound");
            await _dbContext.DidNotReceive().RemoveTournamentGroupAsync(Arg.Any<TournamentGroup>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupHasTeams()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            _teamModule.AnyTeamsInGroupAsync(group.Id, Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var result = await _handler.Handle(new DeleteTournamentGroupCommand(group.TournamentId, group.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.GroupId");
            await _dbContext.DidNotReceive().RemoveTournamentGroupAsync(Arg.Any<TournamentGroup>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupHasMatches()
        {
            // Arrange
            var group = CreateFakeTournamentGroup();
            _dbContext.GetTournamentGroupByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
            _matchModule.AnyMatchesForGroupAsync(group.Id, Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var result = await _handler.Handle(new DeleteTournamentGroupCommand(group.TournamentId, group.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.GroupId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
