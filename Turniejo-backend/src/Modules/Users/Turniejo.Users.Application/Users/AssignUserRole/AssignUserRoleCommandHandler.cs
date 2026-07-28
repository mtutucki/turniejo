using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.Application.Users.AssignUserRole;

internal sealed class AssignUserRoleCommandHandler(
    IUsersDbContext dbContext)
    : ICommandHandler<AssignUserRoleCommand>
{
    public async Task<Result> Handle(
        AssignUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        // Sprawdź czy rola już istnieje
        var exists = await dbContext.UserRoleAssignmentExistsAsync(
            request.UserId,
            request.Role,
            request.ScopeType,
            request.ScopeId,
            cancellationToken);

        if (exists)
            return Result.Success(); // idempotentne — nie błąd

        var result = UserRoleAssignment.Create(
            request.UserId,
            request.Role,
            request.ScopeType,
            request.ScopeId);

        if (result.IsFailure)
            return Result.Failure(result.Error);

        await dbContext.AddUserRoleAssignmentAsync(result.Value, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}