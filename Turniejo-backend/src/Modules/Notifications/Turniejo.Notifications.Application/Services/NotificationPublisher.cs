using System.Text.Json;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Application.Services
{
    public sealed class NotificationPublisher(INotificationsDbContext dbContext)
    {
        public async Task<Result<Notification>> PublishAsync(
            Guid? tournamentId, NotificationType type, string title, string message, Guid? createdByUserId,
            IReadOnlyCollection<Guid> recipientUserIds, Guid? matchId = null, CancellationToken cancellationToken = default)
        {
            var notificationResult = Notification.Create(Guid.NewGuid(), tournamentId, type, title, message, createdByUserId, matchId);
            if (notificationResult.IsFailure)
                return Result.Failure<Notification>(notificationResult.Error);

            var notification = notificationResult.Value;
            await dbContext.AddNotificationAsync(notification, cancellationToken);

            var recipients = recipientUserIds
                .Distinct()
                .Select(userId => NotificationRecipient.Create(Guid.NewGuid(), notification.Id, userId))
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToList();

            if (recipients.Count > 0)
                await dbContext.AddNotificationRecipientsAsync(recipients, cancellationToken);

            foreach (var domainEvent in notification.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            return Result.Success(notification);
        }
    }
}
