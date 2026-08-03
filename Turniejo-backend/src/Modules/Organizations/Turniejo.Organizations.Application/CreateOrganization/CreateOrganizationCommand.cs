using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Organizations.Application.Organizations.CreateOrganization;

public sealed record CreateOrganizationCommand(
    Guid OwnerUserId,
    string Name) : ICommand<CreateOrganizationResponse>;