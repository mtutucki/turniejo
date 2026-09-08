using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamResults
{
    public sealed record GetTeamResultsQuery(Guid TournamentTeamId) : IQuery<IReadOnlyList<TeamMatchDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid ScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.AnyTeamStaff;
    }
}
