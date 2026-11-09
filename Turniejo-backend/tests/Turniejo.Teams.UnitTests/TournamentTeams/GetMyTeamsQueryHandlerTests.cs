using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.GetMyTeams;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetMyTeamsQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetMyTeamsQueryHandler _handler;

        public GetMyTeamsQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new GetMyTeamsQueryHandler(_dbContext, _tournamentModule, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenUserHasNoTeamStaffAssignments()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);
            _dbContext.GetTeamStaffByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TeamStaff>());

            // Act
            var result = await _handler.Handle(new GetMyTeamsQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnTeamsWithResolvedNames_ForCoachStaffAssignments()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);

            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeamId, userId, Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TeamStaff> { staff });

            var tournamentTeam = TournamentTeam.Create(tournamentTeamId, tournamentId, teamId, Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(tournamentTeam);

            var team = Team.Create(teamId, Guid.NewGuid(), Guid.NewGuid(), "Lions", "LIO", null).Value;
            _dbContext.GetTeamByIdAsync(teamId, Arg.Any<CancellationToken>()).Returns(team);

            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 8));

            // Act
            var result = await _handler.Handle(new GetMyTeamsQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].TeamName.Should().Be("Lions");
            result.Value[0].TournamentName.Should().Be("Cup");
            result.Value[0].Role.Should().Be(TeamStaffRole.Coach.ToString());
            result.Value[0].TournamentStatus.Should().Be("InProgress");
            // Brak ustawień turnieju => fail-closed, tak jak SquadEditGuard.
            result.Value[0].CanEditSquad.Should().BeFalse();
        }

        [Theory]
        [InlineData(RegistrationStatus.Withdrawn)]
        [InlineData(RegistrationStatus.Rejected)]
        public async Task Handle_ShouldSkipTeams_ThatWereWithdrawnOrRejected(RegistrationStatus status)
        {
            var userId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);

            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeamId, userId, Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TeamStaff> { staff });
            var tournamentTeam = TournamentTeam.Create(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            if (status == RegistrationStatus.Withdrawn)
                tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Accepted, Guid.NewGuid());
            tournamentTeam.ChangeRegistrationStatus(status, Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(tournamentTeam);

            var result = await _handler.Handle(new GetMyTeamsQuery(), CancellationToken.None);

            tournamentTeam.RegistrationStatus.Should().Be(status);
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Theory]
        [InlineData("Draft", false, true)]
        [InlineData("InProgress", false, false)]
        [InlineData("InProgress", true, true)]
        [InlineData("Finished", false, false)]
        public async Task Handle_ShouldComputeCanEditSquad_FromStatusAndSettings(string status, bool canEditAfterStart, bool expected)
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);

            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeamId, userId, Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TeamStaff> { staff });
            var tournamentTeam = TournamentTeam.Create(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(tournamentTeam);

            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", status, "Group", Guid.NewGuid(), 8));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 20, 5, 10, 3, 1, 0, false, canEditAfterStart, 3, 0));

            // Act
            var result = await _handler.Handle(new GetMyTeamsQuery(), CancellationToken.None);

            // Assert
            result.Value[0].CanEditSquad.Should().Be(expected);
        }
            // QA v3: PAN-001 - drużyna z usuniętego turnieju zostawała w „Moje drużyny” jako „Unknown”.
        [Fact]
        public async Task PAN_001_TeamFromADeletedTournament_IsNotListed()
        {
            var userId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);
            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeamId, userId, Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TeamStaff> { staff });
            var tournamentTeam = TournamentTeam.Create(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _tournamentModule.GetTournamentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentDto?)null);

            var result = await _handler.Handle(new GetMyTeamsQuery(), CancellationToken.None);

            result.Value.Should().BeEmpty();
        }
    }
}
