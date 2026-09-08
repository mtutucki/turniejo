using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Users.Application.Users.RemoveUserRole;

// Jak AssignUserRoleCommand: wołane wyłącznie wewnętrznie, autoryzacja po stronie wywołującego.
public sealed record RemoveUserRoleCommand(
    Guid UserId,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId = null) : ICommand, IPublicRequest;