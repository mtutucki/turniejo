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
        bool ShowLiveMatches) : ICommand<UpdateTournamentPublicSettingsResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
