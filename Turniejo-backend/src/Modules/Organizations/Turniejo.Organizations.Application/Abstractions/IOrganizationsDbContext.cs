using Turniejo.Organizations.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Organizations.Application.Abstractions;

public interface IOrganizationsDbContext
{
    Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<bool> OrganizationExistsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}