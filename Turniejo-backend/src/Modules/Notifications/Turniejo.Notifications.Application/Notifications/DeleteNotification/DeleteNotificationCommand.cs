using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.DeleteNotification
{
    // Handler weryfikuje recipient.UserId == CurrentUserId; usuwa tylko wpis odbiorcy, nie samo powiadomienie.
    public sealed record DeleteNotificationCommand(Guid NotificationRecipientId) : ICommand, IAuthenticatedOnlyRequest;
}
