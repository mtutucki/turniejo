using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Users.Application.Users.AssignUserRole;

public sealed record AssignUserRoleCommand(
    Guid UserId,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId = null) : ICommand;