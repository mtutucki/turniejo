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

    // Lustro Teams.Application.Authorization.TeamRoles.AnyTeamStaff — reszta panelu trenera
    // (harmonogram/wyniki/statystyki drużyny) dopuszcza AssistantCoach/Manager, więc lista
    // zawodników nie może być jedynym widokiem ograniczonym do samego Coacha.
    public static readonly IReadOnlyCollection<string> AnyTeamStaff =
    [
        nameof(UserRole.Coach),
        nameof(UserRole.AssistantCoach),
        nameof(UserRole.Manager)
    ];
}
