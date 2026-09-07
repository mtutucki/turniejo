using Turniejo.SharedKernel.Enums;

namespace Turniejo.Teams.Application.Authorization;

internal static class TeamRoles
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

    public static readonly IReadOnlyCollection<string> AnyTeamStaff =
    [
        nameof(UserRole.Coach),
        nameof(UserRole.AssistantCoach),
        nameof(UserRole.Manager)
    ];
}
