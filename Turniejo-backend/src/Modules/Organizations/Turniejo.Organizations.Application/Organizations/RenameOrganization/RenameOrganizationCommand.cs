using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Organizations.Application.Organizations.RenameOrganization;

public sealed record RenameOrganizationCommand(Guid OrganizationId, string Name) : ICommand, IAuthorizedRequest
{
    public string ScopeType => nameof(RoleScopeType.Organization);
    public Guid ScopeId => OrganizationId;
    public IReadOnlyCollection<string> AllowedRoles => [nameof(UserRole.Organizer)];
}
