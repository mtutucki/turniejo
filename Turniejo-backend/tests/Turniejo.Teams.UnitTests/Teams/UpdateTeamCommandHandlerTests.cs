using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Teams.UpdateTeam;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.Teams
{
    public sealed class UpdateTeamCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly UpdateTeamCommandHandler _handler;

        public UpdateTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _handler = new UpdateTeamCommandHandler(_dbContext);
        }

        private static Team CreateFakeTeam(Guid organizationId)
        {
            var team = Team.Create(Guid.NewGuid(), organizationId, Guid.NewGuid(), "FC Testowy", null, null).Value;
            team.ClearDomainEvents();
            return team;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var team = CreateFakeTeam(organizationId);
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            var command = new UpdateTeamCommand(organizationId, team.Id, "Nowa nazwa", "NN");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            team.Name.Should().Be("Nowa nazwa");
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamNotFound()
        {
            // Arrange
            var teamId = Guid.NewGuid();
            _dbContext.GetTeamByIdAsync(teamId, Arg.Any<CancellationToken>()).Returns((Team?)null);
            var command = new UpdateTeamCommand(Guid.NewGuid(), teamId, "Nowa nazwa", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Team.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamBelongsToDifferentOrganization()
        {
            // Arrange
            var team = CreateFakeTeam(Guid.NewGuid());
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            var command = new UpdateTeamCommand(Guid.NewGuid(), team.Id, "Nowa nazwa", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Team.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
