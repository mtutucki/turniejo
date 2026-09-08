using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamRegistrationStatus;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class ChangeTournamentTeamRegistrationStatusCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ChangeTournamentTeamRegistrationStatusCommandHandler _handler;

        public ChangeTournamentTeamRegistrationStatusCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new ChangeTournamentTeamRegistrationStatusCommandHandler(_dbContext, _currentUserContext);
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
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new ChangeTournamentTeamRegistrationStatusCommand(tournamentId, tournamentTeam.Id, RegistrationStatus.Accepted);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.RegistrationStatus.Should().Be(RegistrationStatus.Accepted);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeam?)null);
            var command = new ChangeTournamentTeamRegistrationStatusCommand(Guid.NewGuid(), tournamentTeamId, RegistrationStatus.Accepted);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamBelongsToDifferentTournament()
        {
            // Arrange
            var tournamentTeam = CreateFakeTournamentTeam(Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new ChangeTournamentTeamRegistrationStatusCommand(Guid.NewGuid(), tournamentTeam.Id, RegistrationStatus.Accepted);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTransitionIsNotAllowed()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId);
            tournamentTeam.ChangeRegistrationStatus(RegistrationStatus.Rejected, Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new ChangeTournamentTeamRegistrationStatusCommand(tournamentId, tournamentTeam.Id, RegistrationStatus.Accepted);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RegistrationStatus");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
