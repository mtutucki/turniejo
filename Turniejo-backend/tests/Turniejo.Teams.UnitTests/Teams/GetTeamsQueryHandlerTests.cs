using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Teams.GetTeams;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.Teams
{
    public sealed class GetTeamsQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly GetTeamsQueryHandler _handler;

        public GetTeamsQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _handler = new GetTeamsQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnOrganizationTeams()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var team = Team.Create(Guid.NewGuid(), organizationId, Guid.NewGuid(), "Orły Warszawa", "ORŁ", null).Value;
            _dbContext.GetTeamsByOrganizationIdAsync(organizationId, Arg.Any<CancellationToken>())
                .Returns(new List<Team> { team });

            // Act
            var result = await _handler.Handle(new GetTeamsQuery(organizationId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(team.Id);
            result.Value[0].Name.Should().Be("Orły Warszawa");
            result.Value[0].ShortName.Should().Be("ORŁ");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenOrganizationHasNoTeams()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            _dbContext.GetTeamsByOrganizationIdAsync(organizationId, Arg.Any<CancellationToken>())
                .Returns(new List<Team>());

            // Act
            var result = await _handler.Handle(new GetTeamsQuery(organizationId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
