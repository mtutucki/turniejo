using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.GetTeamStaff;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetTeamStaffQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly GetTeamStaffQueryHandler _handler;

        public GetTeamStaffQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _handler = new GetTeamStaffQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenStaffExists()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TeamStaff> { staff });

            // Act
            var result = await _handler.Handle(new GetTeamStaffQuery(Guid.NewGuid(), tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(staff.Id);
            result.Value[0].Role.Should().Be(staff.Role.ToString());
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoStaffExists()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TeamStaff>());

            // Act
            var result = await _handler.Handle(new GetTeamStaffQuery(Guid.NewGuid(), tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
