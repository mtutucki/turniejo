using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications
{
    // IPublicRequest tu jest celowe (wołane tylko wewnętrznie przez MatchReminderBackgroundService,
    // nigdy przez HTTP) — NIE dodawaj do tego kontrolera akcji przyjmującej tę komendę, bo pomija
    // każdy check AuthorizationBehavior. Regresja pilnowana przez
    // PublicRequestSafetyTests.CreateMatchReminderNotificationsCommand_ShouldNeverBeAcceptedByAControllerAction.
    public sealed record CreateMatchReminderNotificationsCommand(int MinutesBeforeMatch = 30)
        : ICommand<CreateMatchReminderNotificationsResponse>, IPublicRequest;
}
