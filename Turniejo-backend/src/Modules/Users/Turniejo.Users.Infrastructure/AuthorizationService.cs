using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Infrastructure;

internal sealed class AuthorizationService(IUsersDbContext dbContext) : IAuthorizationService
{
    public async Task<bool> HasAnyRoleAsync(
        Guid userId,
        string scopeType,
        Guid scopeId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var userRoles = await dbContext.GetUserRolesAsync(userId, cancellationToken);

        return userRoles.Any(r =>
            r.ScopeId == scopeId &&
            r.ScopeType.ToString() == scopeType &&
            roles.Contains(r.Role.ToString()));
    }
}
