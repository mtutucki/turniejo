using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.AssignUserToPlayer
{
    internal sealed class AssignUserToPlayerCommandHandler(
        IPlayersDbContext dbContext,
        PlayerAccessGuard playerAccessGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignUserToPlayerCommand, AssignUserToPlayerResponse>
    {
        public async Task<Result<AssignUserToPlayerResponse>> Handle(AssignUserToPlayerCommand request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<AssignUserToPlayerResponse>(Error.NotFound("Player", request.PlayerId));

            if (!await playerAccessGuard.CanEditAsync(player, currentUserContext.UserId, cancellationToken))
                throw new UnauthorizedException();

            var result = player.AssignUser(request.UserId);
            if (result.IsFailure)
                return Result.Failure<AssignUserToPlayerResponse>(result.Error);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignUserToPlayerResponse(player.Id));
        }
    }
}
