using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.Authorization;

internal static class PlayerRoles
{
    public static readonly IReadOnlyCollection<string> OrganizerOnly =
    [
        nameof(UserRole.Organizer)
    ];

    public static readonly IReadOnlyCollection<string> AnyTournamentMember =
    [
        nameof(UserRole.Organizer),
        nameof(UserRole.Referee),
        nameof(UserRole.Coach),
        nameof(UserRole.Viewer)
    ];

    public static readonly IReadOnlyCollection<string> GuardianOnly =
    [
        nameof(UserRole.Parent)
    ];

    public static readonly IReadOnlyCollection<string> CoachOnly =
    [
        nameof(UserRole.Coach)
    ];
}
