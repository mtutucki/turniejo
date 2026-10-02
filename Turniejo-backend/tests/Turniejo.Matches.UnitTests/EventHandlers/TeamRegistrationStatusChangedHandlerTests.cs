using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class TeamRegistrationStatusChangedHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly TeamRegistrationStatusChangedHandler _handler;

        public TeamRegistrationStatusChangedHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new TeamRegistrationStatusChangedHandler(_dbContext, _tournamentModule);
        }

        private static Match CreateScheduledMatch(Guid teamId, StageType stageType = StageType.Group, Guid? tournamentId = null)
        {
            var match = Match.Create(
                Guid.NewGuid(), tournamentId ?? Guid.NewGuid(), null, null, teamId, Guid.NewGuid(), null,
                DateTimeOffset.UtcNow.AddDays(1), stageType).Value;
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldCancelScheduledMatches_WhenTeamWithdraws()
        {
            // Zamyka lukę z audytu: wycofanie drużyny nie blokowało startu/zakończenia jej meczów.
            var teamId = Guid.NewGuid();
            var match = CreateScheduledMatch(teamId);
            _dbContext.GetCancellableMatchesByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns([match]);
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            match.Status.Should().Be(MatchStatus.Cancelled);
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchCancelled")),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        private void SetupTournament(Guid tournamentId, string status)
        {
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Test", status, "Knockout", Guid.NewGuid(), 16));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
        }

        [Fact]
        public async Task Handle_ShouldAwardWalkoverToOpponent_WhenTeamWithdrawsFromKnockoutMatch_InRunningTournament()
        {
            // Odwołany mecz pucharowy nie ma zwycięzcy — drabinka stawała. Rywal awansuje walkowerem.
            var teamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var match = CreateScheduledMatch(teamId, StageType.Knockout, tournamentId);
            SetupTournament(tournamentId, "InProgress");
            _dbContext.GetCancellableMatchesByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns([match]);
            var notification = new TeamRegistrationStatusChanged(teamId, tournamentId, RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            match.Status.Should().Be(MatchStatus.Finished);
            match.ResultType.Should().Be(MatchResultType.Walkover);
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
            match.HomeScore.Should().Be(0);
            match.AwayScore.Should().Be(3);
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchFinished")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCancelKnockoutMatch_WhenTournamentHasNotStarted()
        {
            // Przed startem organizator układa drabinkę od nowa — walkower zablokowałby reset.
            var teamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var match = CreateScheduledMatch(teamId, StageType.Knockout, tournamentId);
            SetupTournament(tournamentId, "ScheduleGenerated");
            _dbContext.GetCancellableMatchesByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns([match]);
            var notification = new TeamRegistrationStatusChanged(teamId, tournamentId, RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            match.Status.Should().Be(MatchStatus.Cancelled);
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenStatusIsNotWithdrawn()
        {
            var teamId = Guid.NewGuid();
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Pending, RegistrationStatus.Accepted, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().GetCancellableMatchesByTournamentTeamIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenNoCancellableMatches()
        {
            var teamId = Guid.NewGuid();
            _dbContext.GetCancellableMatchesByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns([]);
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
