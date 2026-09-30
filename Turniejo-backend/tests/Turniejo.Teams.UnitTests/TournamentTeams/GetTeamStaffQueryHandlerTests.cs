using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.GetTeamStaff;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetTeamStaffQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly GetTeamStaffQueryHandler _handler;

        public GetTeamStaffQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _handler = new GetTeamStaffQueryHandler(_dbContext, _userModule);
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
        public async Task Handle_ShouldIncludeUserDetails_WhenUserExists()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeamId, userId, Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TeamStaff> { staff });
            _userModule.GetUserAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new UserDto(userId, "trener@turniejo.pl", "Jan", "Kowalski"));

            // Act
            var result = await _handler.Handle(new GetTeamStaffQuery(Guid.NewGuid(), tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value[0].Email.Should().Be("trener@turniejo.pl");
            result.Value[0].FirstName.Should().Be("Jan");
            result.Value[0].LastName.Should().Be("Kowalski");
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
