using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.MarkNotificationAsRead
{
    public sealed record MarkNotificationAsReadCommand(Guid NotificationRecipientId) : ICommand<MarkNotificationAsReadResponse>;
}
