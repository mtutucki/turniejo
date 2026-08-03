using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Users.Domain.Entities;

public sealed class UserRoleAssignment : BaseEntity
{
    public Guid UserId { get; private set; }
    public UserRole Role { get; private set; }
    public RoleScopeType ScopeType { get; private set; }
    public Guid? ScopeId { get; private set; }

    private UserRoleAssignment(Guid id) : base(id) { }
    private UserRoleAssignment() { }

    public static Result<UserRoleAssignment> Create(
        Guid userId,
        UserRole role,
        RoleScopeType scopeType,
        Guid? scopeId = null)
    {
        if (userId == Guid.Empty)
            return Result.Failure<UserRoleAssignment>(
                Error.Validation("UserId", "UserId cannot be empty."));

        var assignment = new UserRoleAssignment(Guid.NewGuid())
        {
            UserId = userId,
            Role = role,
            ScopeType = scopeType,
            ScopeId = scopeId
        };

        return Result.Success(assignment);
    }
}