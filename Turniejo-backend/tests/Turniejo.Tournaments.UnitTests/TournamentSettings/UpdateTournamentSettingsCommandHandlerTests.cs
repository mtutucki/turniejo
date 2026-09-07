using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.TournamentSetting.UpdateTournamentSettings;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.TournamentSettings
{
    public sealed class UpdateTournamentSettingsCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly UpdateTournamentSettingsCommandHandler _handler;

        public UpdateTournamentSettingsCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new UpdateTournamentSettingsCommandHandler(_dbContext, _currentUserContext);
        }

        private static Tournament CreateFakeTournament()
        {
            var tournament = Tournament.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Turniej testowy",
                DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
                8, null).Value;

            tournament.ClearDomainEvents();
            return tournament;
        }

        private static UpdateTournamentSettingsCommand CreateValidCommand(Guid tournamentId) =>
            new(tournamentId, 3, 1, 0, 30, 5, false, 60, 3, 0, false);

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
            var command = CreateValidCommand(tournament.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentId.Should().Be(tournament.Id);
            tournament.Settings.PointsForWin.Should().Be(command.PointsForWin);
            tournament.Settings.WalkoverHomeScore.Should().Be(command.WalkoverHomeScore);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns((Tournament?)null);
            var command = CreateValidCommand(tournamentId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("tournament.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentIsInProgress()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            tournament.ChangeStatus(TournamentStatus.RegistrationOpen, tournament.CreatedByUserId);
            tournament.ChangeStatus(TournamentStatus.RegistrationClosed, tournament.CreatedByUserId);
            tournament.SetFormat(TournamentFormat.Group);
            tournament.ChangeStatus(TournamentStatus.ScheduleGenerated, tournament.CreatedByUserId);
            tournament.ChangeStatus(TournamentStatus.InProgress, tournament.CreatedByUserId);

            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
            var command = CreateValidCommand(tournament.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenSettingsAreInvalid()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
            var command = new UpdateTournamentSettingsCommand(tournament.Id, 3, 1, 0, 30, 5, false, 60, 2, 2, false);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.WalkoverHomeScore");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
