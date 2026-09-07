using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.AssignUserToTournamentTeam
{
    public sealed record AssignUserToTournamentTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid UserId,
        TeamStaffRole Role) : ICommand<AssignUserToTournamentTeamResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
