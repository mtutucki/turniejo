using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.Teams.SetTeamLabels
{
    /// <summary>Etykiety jednej drużyny (okno edycji drużyny). Pusta lista zdejmuje wszystkie.</summary>
    public sealed record SetTeamLabelsCommand(Guid OrganizationId, Guid TeamId, IReadOnlyCollection<Guid> LabelIds) : ICommand, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
