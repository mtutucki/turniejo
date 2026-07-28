using Turniejo.SharedKernel.Outbox;
using Turniejo.Users.Domain.Entities;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Abstractions;

public interface IUsersDbContext
{
    Task AddUserAsync(User user, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task AddUserRoleAssignmentAsync(UserRoleAssignment assignment, CancellationToken cancellationToken = default);
    Task<bool> UserRoleAssignmentExistsAsync(Guid userId, UserRole role, RoleScopeType scopeType, Guid? scopeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRoleAssignment>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RemoveUserRoleAssignmentAsync(Guid userId, UserRole role, RoleScopeType scopeType, Guid? scopeId, CancellationToken cancellationToken = default);

}