using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Organizations.Application.Organizations.CreateOrganization;

// OwnerUserId pochodzi z ICurrentUserContext po stronie kontrolera, nie z ciała żądania — każdy
// zalogowany user może utworzyć WŁASNĄ organizację, unikalny indeks na OwnerUserId pilnuje reszty.
public sealed record CreateOrganizationCommand(
    Guid OwnerUserId,
    string Name) : ICommand<CreateOrganizationResponse>, IAuthenticatedOnlyRequest;