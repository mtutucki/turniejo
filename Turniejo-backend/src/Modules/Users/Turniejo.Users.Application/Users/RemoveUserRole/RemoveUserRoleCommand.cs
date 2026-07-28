using Turniejo.SharedKernel.CQRS;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.RemoveUserRole;

public sealed record RemoveUserRoleCommand(
    Guid UserId,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId = null) : ICommand;