namespace Turniejo.Users.Application.Abstractions;

public interface IUsersDbContext
{
    Task AddUserAsync(Domain.Entities.User user, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(SharedKernel.Outbox.OutboxMessage message, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}