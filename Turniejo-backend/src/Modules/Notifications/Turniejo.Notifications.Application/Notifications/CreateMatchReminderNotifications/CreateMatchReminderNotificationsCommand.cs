using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications
{
    public sealed record CreateMatchReminderNotificationsCommand(int MinutesBeforeMatch = 30)
        : ICommand<CreateMatchReminderNotificationsResponse>, IPublicRequest;
}
