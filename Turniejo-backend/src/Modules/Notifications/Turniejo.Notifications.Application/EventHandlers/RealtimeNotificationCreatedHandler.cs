using MediatR;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.EventHandlers
{
    // Zdarzenie NotificationCreated istniało, ale nic go nie wysyłało — dzwonek w aplikacji pokazywał nowe
    // powiadomienia dopiero po odświeżeniu. Wysyłamy je do kont odbiorców (NotificationHub, Clients.Users).
    internal sealed class RealtimeNotificationCreatedHandler(
        INotificationsDbContext dbContext,
        IRealtimeService realtimeService) : INotificationHandler<NotificationCreated>
    {
        public async Task Handle(NotificationCreated notification, CancellationToken cancellationToken)
        {
            var created = await dbContext.GetNotificationByIdAsync(notification.NotificationId, cancellationToken);
            if (created == null)
                return;

            var recipients = await dbContext.GetNotificationRecipientsByNotificationIdAsync(notification.NotificationId, cancellationToken);
            if (recipients.Count == 0)
                return;

            await realtimeService.PublishNotificationCreatedAsync(
                recipients.Select(r => r.UserId).Distinct().ToList(),
                created.Id, created.Type.ToString(), created.Title, created.Message, cancellationToken);
        }
    }
}
