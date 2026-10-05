using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.MarkNotificationAsRead
{
    // Handler weryfikuje recipient.UserId == CurrentUserId przed oznaczeniem jako przeczytane.
    public sealed record MarkNotificationAsReadCommand(Guid NotificationRecipientId) : ICommand<MarkNotificationAsReadResponse>, IAuthenticatedOnlyRequest;
}
