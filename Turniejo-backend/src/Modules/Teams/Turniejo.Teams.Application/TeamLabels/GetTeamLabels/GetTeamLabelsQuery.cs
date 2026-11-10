using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TeamLabels.GetTeamLabels
{
    public sealed record GetTeamLabelsQuery(Guid OrganizationId) : IQuery<IReadOnlyList<TeamLabelDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
