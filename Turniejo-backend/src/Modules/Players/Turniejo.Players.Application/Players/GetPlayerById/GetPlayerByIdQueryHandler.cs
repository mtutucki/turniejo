using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.GetPlayerById
{
    internal sealed class GetPlayerByIdQueryHandler(
        IPlayersDbContext dbContext,
        PlayerAccessGuard playerAccessGuard,
        Turniejo.SharedKernel.Contracts.ICurrentUserContext currentUserContext) : IQueryHandler<GetPlayerByIdQuery, PlayerDto>
    {
        public async Task<Result<PlayerDto>> Handle(GetPlayerByIdQuery request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<PlayerDto>(Error.NotFound("Player", request.PlayerId));

            if (!await playerAccessGuard.CanViewAsync(player, currentUserContext.UserId, cancellationToken))
                throw new UnauthorizedException();

            var dto = new PlayerDto(
                player.Id, player.UserId, player.FirstName, player.LastName,
                player.BirthDate, player.DefaultPosition.ToString(), player.PhotoUrl);

            return Result.Success(dto);
        }
    }
}
