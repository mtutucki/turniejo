using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.RemoveStaffFromTeam;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class RemoveStaffFromTeamCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly RemoveStaffFromTeamCommandHandler _handler;

        public RemoveStaffFromTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _handler = new RemoveStaffFromTeamCommandHandler(_dbContext, _userModule);
        }

        private TournamentTeam SetUpTournamentTeam(Guid tournamentId)
        {
            var tournamentTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            return tournamentTeam;
        }

        [Fact]
        public async Task Handle_ShouldRemoveStaffAndRevokeTeamRole_WhenStaffBelongsToTeam()
        {
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = SetUpTournamentTeam(tournamentId);
            var coachUserId = Guid.NewGuid();
            var staff = TeamStaff.CreateCoach(Guid.NewGuid(), tournamentTeam.Id, coachUserId, Guid.NewGuid()).Value;
            _dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>())
                .Returns(new List<TeamStaff> { staff });

            var result = await _handler.Handle(
                new RemoveStaffFromTeamCommand(tournamentId, tournamentTeam.Id, staff.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemoveTeamStaffAsync(staff, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
            await _userModule.Received(1).RevokeRoleAsync(
                coachUserId, nameof(UserRole.Coach), nameof(RoleScopeType.TournamentTeam), tournamentTeam.Id, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamBelongsToDifferentTournament()
        {
            var tournamentTeam = SetUpTournamentTeam(Guid.NewGuid());

            var result = await _handler.Handle(
                new RemoveStaffFromTeamCommand(Guid.NewGuid(), tournamentTeam.Id, Guid.NewGuid()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
            await _dbContext.DidNotReceive().RemoveTeamStaffAsync(Arg.Any<TeamStaff>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_AndKeepRoles_WhenStaffIsNotOnThisTeam()
        {
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = SetUpTournamentTeam(tournamentId);
            _dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>())
                .Returns(new List<TeamStaff>());

            var result = await _handler.Handle(
                new RemoveStaffFromTeamCommand(tournamentId, tournamentTeam.Id, Guid.NewGuid()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TeamStaff.NotFound");
            await _userModule.DidNotReceive().RevokeRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
