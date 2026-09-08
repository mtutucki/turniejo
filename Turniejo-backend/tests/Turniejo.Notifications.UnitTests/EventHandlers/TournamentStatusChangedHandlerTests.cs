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
        private readonly TournamentStatusChangedHandler _handler;

        public TournamentStatusChangedHandlerTests()
        {
            _dbContext = Substitute.For<INotificationsDbContext>();
            _userModule = Substitute.For<IUserModule>();

            var publisher = new NotificationPublisher(_dbContext);

            _handler = new TournamentStatusChangedHandler(publisher, _userModule, _dbContext);
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
    }
}
