using Turniejo.SharedKernel.Outbox;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.Application.Abstractions;

public interface IUsersDbContext
{
    Task AddUserAsync(User user, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}