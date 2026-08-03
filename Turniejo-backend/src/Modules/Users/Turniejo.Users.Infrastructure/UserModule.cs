using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.AssignUserRole;

namespace Turniejo.Users.Infrastructure;

internal sealed class UserModule(
    IUsersDbContext dbContext,
    ISender sender) : IUserModule
{
    public async Task<UserDto?> GetUserAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
    {
        var user = await dbContext.GetUserByIdAsync(userId, cancellationToken);
        if (user is null) return null;
        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName);
    }

    public async Task<IReadOnlyList<UserRoleDto>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.GetUserRolesAsync(userId, cancellationToken);
        return roles.Select(r => new UserRoleDto(
            r.UserId,
            r.Role.ToString(),
            r.ScopeType.ToString(),
            r.ScopeId)).ToList();
    }

    public async Task AssignRoleAsync(
        Guid userId,
        string role,
        string scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var userRole = Enum.Parse<UserRole>(role);
        var roleScopeType = Enum.Parse<RoleScopeType>(scopeType);

        await sender.Send(new AssignUserRoleCommand(
            userId, userRole, roleScopeType, scopeId),
            cancellationToken);
    }
}