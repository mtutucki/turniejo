using Turniejo.Notifications.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Notifications.Application.Abstractions
{
    public interface INotificationsDbContext
    {
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Notification
        Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
        Task<Notification?> GetNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);
        // Przypomnienie wysłane od createdSince (okno przed aktualnym terminem). Starsze dotyczyło terminu sprzed przesunięcia.
        Task<bool> MatchReminderExistsAsync(Guid matchId, DateTimeOffset createdSince, CancellationToken cancellationToken = default);

        // NotificationRecipient
        Task AddNotificationRecipientAsync(NotificationRecipient recipient, CancellationToken cancellationToken = default);
        Task AddNotificationRecipientsAsync(IReadOnlyCollection<NotificationRecipient> recipients, CancellationToken cancellationToken = default);
        Task<NotificationRecipient?> GetNotificationRecipientByIdAsync(Guid notificationRecipientId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NotificationRecipient>> GetNotificationRecipientsByNotificationIdAsync(Guid notificationId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NotificationRecipient>> GetNotificationRecipientsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        void RemoveNotificationRecipient(NotificationRecipient recipient);
    }
}
