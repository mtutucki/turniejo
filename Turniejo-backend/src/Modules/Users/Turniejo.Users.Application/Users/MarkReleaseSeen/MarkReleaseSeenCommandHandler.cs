using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.MarkReleaseSeen;

internal sealed class MarkReleaseSeenCommandHandler(
    IUsersDbContext dbContext,
    ICurrentUserContext currentUserContext) : ICommandHandler<MarkReleaseSeenCommand>
{
    public async Task<Result> Handle(MarkReleaseSeenCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.GetUserByIdAsync(currentUserContext.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("User", currentUserContext.UserId));

        var result = user.MarkReleaseSeen(request.Version);
        if (result.IsFailure)
            return result;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
