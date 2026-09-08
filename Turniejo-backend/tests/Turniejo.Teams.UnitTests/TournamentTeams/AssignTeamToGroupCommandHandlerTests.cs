using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class AssignTeamToGroupCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly AssignTeamToGroupCommandHandler _handler;

        public AssignTeamToGroupCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _handler = new AssignTeamToGroupCommandHandler(_dbContext);
        }

        private static TournamentTeam CreateFakeTournamentTeam(Guid tournamentId)
        {
            var tournamentTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            tournamentTeam.ClearDomainEvents();
            return tournamentTeam;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId);
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeam.Id, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.GroupId.Should().Be(groupId);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeam?)null);
            var command = new AssignTeamToGroupCommand(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }
    }
}
