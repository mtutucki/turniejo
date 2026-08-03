using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.RemoveUserRole;

internal sealed class RemoveUserRoleCommandHandler(
    IUsersDbContext dbContext)
    : ICommandHandler<RemoveUserRoleCommand>
{
    public async Task<Result> Handle(
        RemoveUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        await dbContext.RemoveUserRoleAssignmentAsync(
            request.UserId,
            request.Role,
            request.ScopeType,
            request.ScopeId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}