using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetPlayerGuardians
{
    internal sealed class GetPlayerGuardiansQueryHandler(IPlayersDbContext dbContext) : IQueryHandler<GetPlayerGuardiansQuery, IReadOnlyList<PlayerGuardianDto>>
    {
        public async Task<Result<IReadOnlyList<PlayerGuardianDto>>> Handle(GetPlayerGuardiansQuery request, CancellationToken cancellationToken)
        {
            var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(request.PlayerId, cancellationToken);

            var dtos = guardians.Select(g => new PlayerGuardianDto(
                g.Id, g.PlayerId, g.UserId, g.RelationshipType.ToString(), g.CanEdit, g.CanReceiveNotifications)).ToList();

            return Result.Success<IReadOnlyList<PlayerGuardianDto>>(dtos);
        }
    }
}
