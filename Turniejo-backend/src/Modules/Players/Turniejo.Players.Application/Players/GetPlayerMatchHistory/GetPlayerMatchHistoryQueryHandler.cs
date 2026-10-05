using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.GetPlayerMatchHistory
{
    internal sealed class GetPlayerMatchHistoryQueryHandler(
        IPlayersDbContext dbContext,
        MyChildMatchResolver matchResolver,
        PlayerAccessGuard playerAccessGuard,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetPlayerMatchHistoryQuery, IReadOnlyList<MyChildMatchDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildMatchDto>>> Handle(GetPlayerMatchHistoryQuery request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<IReadOnlyList<MyChildMatchDto>>(Error.NotFound("Player", request.PlayerId));

            if (!await playerAccessGuard.CanViewAsync(player, currentUserContext.UserId, cancellationToken))
                throw new UnauthorizedException();

            var matches = await matchResolver.ResolveAsync(request.PlayerId, m => m.Status == "Finished", cancellationToken);

            return Result.Success(matches);
        }
    }
}
