using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TeamLabels.CreateTeamLabel
{
    public sealed record CreateTeamLabelCommand(Guid OrganizationId, string Name) : ICommand<CreateTeamLabelResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }

    public sealed record CreateTeamLabelResponse(Guid LabelId);
}
