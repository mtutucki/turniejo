using Turniejo.Notifications.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.GetMyNotifications
{
    public sealed record GetMyNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>>;
}
