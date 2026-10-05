using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Organizations.Application.Organizations.GetMyOrganization
{
    // "Moja" nie "moje" — V1 świadomie ogranicza 1 user = 1 organizacja (IX_organizations_owner),
    // więc to zawsze co najwyżej jeden wynik, nigdy lista.
    public sealed record GetMyOrganizationQuery : IQuery<GetMyOrganizationResponse?>, IAuthenticatedOnlyRequest;
}
