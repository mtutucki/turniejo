using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TeamLabels.SetTeamLabelMembers
{
    /// <summary>Ustawia, które drużyny z bazy mają etykietę: te z listy ją dostają, pozostałe tracą.</summary>
    public sealed record SetTeamLabelMembersCommand(Guid OrganizationId, Guid LabelId, IReadOnlyCollection<Guid> TeamIds) : ICommand, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
