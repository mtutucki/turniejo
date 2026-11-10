using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TeamLabels.DeleteTeamLabel
{
    /// <summary>Usuwa etykietę i zdejmuje ją ze wszystkich drużyn. Drużyny zostają.</summary>
    public sealed record DeleteTeamLabelCommand(Guid OrganizationId, Guid LabelId) : ICommand, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
