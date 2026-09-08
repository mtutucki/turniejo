using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.GetPlayerById
{
    internal sealed class GetPlayerByIdQueryHandler(IPlayersDbContext dbContext) : IQueryHandler<GetPlayerByIdQuery, PlayerDto>
    {
        public async Task<Result<PlayerDto>> Handle(GetPlayerByIdQuery request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<PlayerDto>(Error.NotFound("Player", request.PlayerId));

            var dto = new PlayerDto(
                player.Id, player.UserId, player.FirstName, player.LastName,
                player.BirthDate, player.DefaultPosition.ToString(), player.PhotoUrl);

            return Result.Success(dto);
        }
    }
}
