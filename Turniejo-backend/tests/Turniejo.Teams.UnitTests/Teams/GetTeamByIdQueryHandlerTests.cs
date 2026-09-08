using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Teams.GetTeamById;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.Teams
{
    public sealed class GetTeamByIdQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly GetTeamByIdQueryHandler _handler;

        public GetTeamByIdQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _handler = new GetTeamByIdQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDto_WhenTeamExists()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var team = Team.Create(Guid.NewGuid(), organizationId, Guid.NewGuid(), "FC Testowy", "FCT", "logo.png").Value;
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

            // Act
            var result = await _handler.Handle(new GetTeamByIdQuery(organizationId, team.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(team.Id);
            result.Value.Name.Should().Be(team.Name);
            result.Value.ShortName.Should().Be(team.ShortName);
            result.Value.LogoUrl.Should().Be(team.LogoUrl);
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenTeamNotExist()
        {
            // Arrange
            var teamId = Guid.NewGuid();
            _dbContext.GetTeamByIdAsync(teamId, Arg.Any<CancellationToken>()).Returns((Team?)null);

            // Act
            var result = await _handler.Handle(new GetTeamByIdQuery(Guid.NewGuid(), teamId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Team.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenTeamBelongsToDifferentOrganization()
        {
            // Arrange
            var team = Team.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FC Testowy", null, null).Value;
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

            // Act
            var result = await _handler.Handle(new GetTeamByIdQuery(Guid.NewGuid(), team.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Team.NotFound");
        }
    }
}
