using Turniejo.SharedKernel.Enums;

namespace Turniejo.Notifications.Application.Authorization;

internal static class NotificationRoles
{
    public static readonly IReadOnlyCollection<string> OrganizerOnly =
    [
        nameof(UserRole.Organizer)
    ];
}
