using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.GetUserRoles;

public sealed record GetUserRolesQuery(Guid UserId)
    : IQuery<IReadOnlyList<UserRoleDto>>;

public sealed record UserRoleDto(
    Guid Id,
    Guid UserId,
    string Role,
    string ScopeType,
    Guid? ScopeId,
    DateTimeOffset CreatedAt);