using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Notifications.UnitTests.EventHandlers
{
    public sealed class TournamentStatusChangedHandlerTests
    {
        private readonly INotificationsDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ITeamModule _teamModule;
        private readonly IMatchModule _matchModule;
        private readonly IPlayerModule _playerModule;
        private readonly TournamentStatusChangedHandler _handler;

        public TournamentStatusChangedHandlerTests()
        {
            _dbContext = Substitute.For<INotificationsDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _matchModule = Substitute.For<IMatchModule>();
            _playerModule = Substitute.For<IPlayerModule>();

            _teamModule.GetTournamentTeamsByTournamentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);
            _matchModule.GetMatchesByTournamentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);

            var publisher = new NotificationPublisher(_dbContext);
            var participantResolver = new TournamentParticipantResolver(_userModule, _teamModule, _matchModule, _playerModule);

            _handler = new TournamentStatusChangedHandler(publisher, participantResolver, _dbContext);
        }

        [Fact]
        public async Task Handle_ShouldPublishToEveryTournamentMember_WhenCancelled()
        {
            // Zamyka lukę z audytu: anulowanie turnieju nie generowało żadnego powiadomienia.
            var tournamentId = Guid.NewGuid();
            var recipientIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>()).Returns(recipientIds);
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.InProgress, TournamentStatus.Cancelled);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).AddNotificationAsync(Arg.Any<Turniejo.Notifications.Domain.Entities.Notification>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenNewStatusIsNotCancelled()
        {
            var tournamentId = Guid.NewGuid();
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.Draft, TournamentStatus.RegistrationOpen);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _userModule.DidNotReceive().GetUserIdsByScopeAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenNoRecipients()
        {
            var tournamentId = Guid.NewGuid();
            _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Guid>());
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.InProgress, TournamentStatus.Cancelled);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSkipUserWhoCancelledTheTournament()
        {
            // Organizator, który sam anulował turniej, nie dostaje o tym powiadomienia — współorganizator tak.
            var tournamentId = Guid.NewGuid();
            var cancellingOrganizerId = Guid.NewGuid();
            var otherOrganizerId = Guid.NewGuid();
            _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Guid> { cancellingOrganizerId, otherOrganizerId });
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), cancellingOrganizerId, TournamentStatus.InProgress, TournamentStatus.Cancelled);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).AddNotificationRecipientsAsync(
                Arg.Is<IReadOnlyCollection<Turniejo.Notifications.Domain.Entities.NotificationRecipient>>(r => r.Count == 1 && r.Single().UserId == otherOrganizerId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAlsoReachRefereesAssignedToTournamentMatches()
        {
            // Zamyka lukę z audytu: sędziowie mają rolę Match-scoped, nie Tournament-scoped —
            // GetUserIdsByScopeAsync("Tournament", ...) samo ich nigdy nie zwróci.
            var tournamentId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            var refereeId = Guid.NewGuid();

            _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Guid>());
            _matchModule.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns([new TournamentMatchDto(matchId, tournamentId, "Scheduled", DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), 0, 0)]);
            _userModule.GetUserIdsByScopeAsync("Match", matchId, Arg.Any<CancellationToken>())
                .Returns(new List<Guid> { refereeId });

            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.InProgress, TournamentStatus.Cancelled);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).AddNotificationRecipientsAsync(
                Arg.Is<IReadOnlyCollection<Turniejo.Notifications.Domain.Entities.NotificationRecipient>>(r => r.Count == 1 && r.Single().UserId == refereeId),
                Arg.Any<CancellationToken>());
        }
    }
}
