using NSubstitute;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.Notifications.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.EventHandlers
{
    public sealed class RealtimeNotificationCreatedHandlerTests
    {
        private readonly INotificationsDbContext _dbContext = Substitute.For<INotificationsDbContext>();
        private readonly IRealtimeService _realtimeService = Substitute.For<IRealtimeService>();
        private readonly RealtimeNotificationCreatedHandler _handler;

        public RealtimeNotificationCreatedHandlerTests()
        {
            _handler = new RealtimeNotificationCreatedHandler(_dbContext, _realtimeService);
        }

        [Fact]
        public async Task Handle_ShouldPushNotificationToEveryRecipient()
        {
            var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.OrganizerAnnouncement, "Zmiana", "Mecze od 10:00", Guid.NewGuid()).Value;
            var parent = Guid.NewGuid();
            var coach = Guid.NewGuid();
            _dbContext.GetNotificationByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
            _dbContext.GetNotificationRecipientsByNotificationIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(new List<NotificationRecipient>
            {
                NotificationRecipient.Create(Guid.NewGuid(), notification.Id, parent).Value,
                NotificationRecipient.Create(Guid.NewGuid(), notification.Id, coach).Value,
            });

            await _handler.Handle(new NotificationCreated(notification.Id, notification.TournamentId, "OrganizerAnnouncement"), CancellationToken.None);

            await _realtimeService.Received(1).PublishNotificationCreatedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(parent) && ids.Contains(coach)),
                notification.Id, "OrganizerAnnouncement", "Zmiana", "Mecze od 10:00", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenNoRecipients()
        {
            var notification = Notification.Create(Guid.NewGuid(), null, NotificationType.MatchStarted, "Start", "Mecz ruszył", null).Value;
            _dbContext.GetNotificationByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
            _dbContext.GetNotificationRecipientsByNotificationIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(new List<NotificationRecipient>());

            await _handler.Handle(new NotificationCreated(notification.Id, null, "MatchStarted"), CancellationToken.None);

            await _realtimeService.DidNotReceiveWithAnyArgs().PublishNotificationCreatedAsync(default!, default, default!, default!, default!, default);
        }
    }
}
