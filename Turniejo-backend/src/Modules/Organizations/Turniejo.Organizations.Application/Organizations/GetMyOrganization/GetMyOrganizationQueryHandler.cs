using Turniejo.Organizations.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Organizations.Application.Organizations.GetMyOrganization
{
    internal sealed class GetMyOrganizationQueryHandler(
        IOrganizationsDbContext dbContext,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyOrganizationQuery, GetMyOrganizationResponse?>
    {
        public async Task<Result<GetMyOrganizationResponse?>> Handle(GetMyOrganizationQuery request, CancellationToken cancellationToken)
        {
            var organization = await dbContext.GetOrganizationByOwnerUserIdAsync(currentUserContext.UserId, cancellationToken);

            // Brak organizacji to legalny, oczekiwany stan (np. tuż po rejestracji, zanim outbox
            // dogoni UserRegisteredHandler) — nie NotFound, tylko null w odpowiedzi 200.
            var response = organization == null
                ? null
                : new GetMyOrganizationResponse(organization.Id, organization.Name, organization.Slug);

            return Result.Success(response);
        }
    }
}
