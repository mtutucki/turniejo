using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournamentPublicSettings
{
    public sealed record UpdateTournamentPublicSettingsCommand(
        Guid TournamentId,
        bool IsPublic,
        bool ShowTeams,
        bool ShowPlayers,
        bool ShowSchedule,
        bool ShowStandings,
        bool ShowBracket,
        bool ShowLiveMatches,
        bool ShowStats,
        bool AbbreviatePlayerNames = true,
        // Wartości, które formularz wczytał. Inne w bazie = ktoś zapisał portal w międzyczasie (QA v3: UST-015).
        ExpectedPublicSettings? Expected = null,
        bool ShowInDirectory = true) : ICommand<UpdateTournamentPublicSettingsResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }

    public sealed record ExpectedPublicSettings(
        bool IsPublic,
        bool ShowTeams,
        bool ShowPlayers,
        bool ShowSchedule,
        bool ShowStandings,
        bool ShowBracket,
        bool ShowLiveMatches,
        bool ShowStats,
        bool AbbreviatePlayerNames,
        bool ShowInDirectory = true);
}
