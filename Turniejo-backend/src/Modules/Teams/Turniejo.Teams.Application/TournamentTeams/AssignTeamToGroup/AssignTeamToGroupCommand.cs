using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup
{
    public sealed record AssignTeamToGroupCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid? GroupId) : ICommand<AssignTeamToGroupResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
