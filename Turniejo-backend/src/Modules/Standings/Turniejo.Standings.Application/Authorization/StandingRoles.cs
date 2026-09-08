using Turniejo.SharedKernel.Enums;

namespace Turniejo.Standings.Application.Authorization;

internal static class StandingRoles
{
    public static readonly IReadOnlyCollection<string> AnyTournamentMember =
    [
        nameof(UserRole.Organizer),
        nameof(UserRole.Referee),
        nameof(UserRole.Coach),
        nameof(UserRole.Viewer)
    ];
}
