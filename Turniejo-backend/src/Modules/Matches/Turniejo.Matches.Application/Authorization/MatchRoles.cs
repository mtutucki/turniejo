using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Authorization;

internal static class MatchRoles
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

    public static readonly IReadOnlyCollection<string> RefereeOnly =
    [
        nameof(UserRole.Referee)
    ];
}
