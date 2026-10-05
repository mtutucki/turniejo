using Turniejo.Notifications.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.GetMyNotifications
{
    // Handler filtruje po NotificationRecipient.UserId == CurrentUserId — brak parametrów wejściowych.
    public sealed record GetMyNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>>, IAuthenticatedOnlyRequest;
}
