using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TournamentTeams.RemoveStaffFromTeam
{
    public sealed record RemoveStaffFromTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid TeamStaffId) : ICommand<RemoveStaffFromTeamResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
