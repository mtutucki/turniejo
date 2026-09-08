using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamPaymentStatus;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class ChangeTournamentTeamPaymentStatusCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ChangeTournamentTeamPaymentStatusCommandHandler _handler;

        public ChangeTournamentTeamPaymentStatusCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new ChangeTournamentTeamPaymentStatusCommandHandler(_dbContext, _currentUserContext);
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
            var command = new ChangeTournamentTeamPaymentStatusCommand(tournamentId, tournamentTeam.Id, PaymentStatus.Paid);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.PaymentStatus.Should().Be(PaymentStatus.Paid);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeam?)null);
            var command = new ChangeTournamentTeamPaymentStatusCommand(Guid.NewGuid(), tournamentTeamId, PaymentStatus.Paid);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamBelongsToDifferentTournament()
        {
            // Ten sam branch co "not found" (TournamentTeam.TournamentId musi się zgadzać z
            // TournamentId z route'a), ale osobny scenariusz — bez tego request mógłby wskazać
            // TournamentTeamId z zupełnie innego turnieju i wciąż trafić w istniejący rekord.
            var tournamentTeam = CreateFakeTournamentTeam(Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new ChangeTournamentTeamPaymentStatusCommand(Guid.NewGuid(), tournamentTeam.Id, PaymentStatus.Paid);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenStatusIsSame()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId);
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new ChangeTournamentTeamPaymentStatusCommand(tournamentId, tournamentTeam.Id, PaymentStatus.NotRequired);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PaymentStatus");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
