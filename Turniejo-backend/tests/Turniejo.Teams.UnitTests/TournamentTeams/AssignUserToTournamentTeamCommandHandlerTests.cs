using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.AssignUserToTournamentTeam;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class AssignUserToTournamentTeamCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AssignUserToTournamentTeamCommandHandler _handler;

        public AssignUserToTournamentTeamCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new AssignUserToTournamentTeamCommandHandler(_dbContext, _userModule, _currentUserContext);
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
            var userId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _dbContext.TeamStaffExistsAsync(tournamentTeam.Id, userId, TeamStaffRole.Manager, Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AssignUserToTournamentTeamCommand(tournamentId, tournamentTeam.Id, userId, TeamStaffRole.Manager);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddTeamStaffAsync(Arg.Any<TeamStaff>(), Arg.Any<CancellationToken>());
            await _userModule.Received(1).AssignRoleAsync(
                userId, nameof(UserRole.Manager), nameof(RoleScopeType.TournamentTeam), tournamentTeam.Id, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeam?)null);
            var command = new AssignUserToTournamentTeamCommand(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), TeamStaffRole.Coach);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamBelongsToDifferentTournament()
        {
            // Arrange
            var tournamentTeam = CreateFakeTournamentTeam(Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new AssignUserToTournamentTeamCommand(Guid.NewGuid(), tournamentTeam.Id, Guid.NewGuid(), TeamStaffRole.Coach);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenUserAlreadyHasThisRole()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId);
            var userId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _dbContext.TeamStaffExistsAsync(tournamentTeam.Id, userId, TeamStaffRole.Coach, Arg.Any<CancellationToken>()).Returns(true);
            var command = new AssignUserToTournamentTeamCommand(tournamentId, tournamentTeam.Id, userId, TeamStaffRole.Coach);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.UserId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
