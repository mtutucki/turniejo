using Turniejo.SharedKernel.CQRS;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.AssignUserRole;

public sealed record AssignUserRoleCommand(
    Guid UserId,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId = null) : ICommand;