using System.Text.Json;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.Organizations.Domain.Events;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Organizations.Application.Services
{
    // Wspólna logika tworzenia organizacji, użyta zarówno przez publiczną CreateOrganizationCommand
    // (POST /api/organizations, wymaga zalogowanego usera) jak i przez UserRegisteredHandler
    // (Outbox, brak HttpContext/usera). Trzymana poza MediatR, żeby ścieżka systemowa nie musiała
    // przechodzić przez AuthorizationBehavior, który wymaga ICurrentUserContext.IsAuthenticated.
    public sealed class OrganizationProvisioner(IOrganizationsDbContext dbContext)
    {
        public async Task<Result<Organization>> CreateAsync(Guid ownerUserId, string name, CancellationToken cancellationToken = default)
        {
            var exists = await dbContext.OrganizationExistsForOwnerAsync(ownerUserId, cancellationToken);
            if (exists)
                return Result.Failure<Organization>(Error.Validation("Organization", "You already have an organization."));

            var result = Organization.Create(ownerUserId, name);
            if (result.IsFailure)
                return result;

            var organization = result.Value;

            await dbContext.AddOrganizationAsync(organization, cancellationToken);

            var outboxMessage = OutboxMessage.Create(
                typeof(OrganizationCreated).AssemblyQualifiedName!,
                JsonSerializer.Serialize(new OrganizationCreated(
                    organization.Id,
                    organization.OwnerUserId,
                    organization.Name)));

            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);

            return Result.Success(organization);
        }
    }
}
