using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TournamentTeams.AssignCoachToTeam
{
    public sealed record AssignCoachToTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid UserId) : ICommand<AssignCoachToTeamResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
