using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildren
{
    internal sealed class GetMyChildrenQueryHandler(
        IPlayersDbContext dbContext,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyChildrenQuery, IReadOnlyList<MyChildDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildDto>>> Handle(GetMyChildrenQuery request, CancellationToken cancellationToken)
        {
            var guardianships = await dbContext.GetGuardianshipsByUserIdAsync(currentUserContext.UserId, cancellationToken);
            if (guardianships.Count == 0)
                return Result.Success<IReadOnlyList<MyChildDto>>([]);

            var playerIds = guardianships.Select(g => g.PlayerId).ToList();
            var players = await dbContext.GetPlayersByIdsAsync(playerIds, cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var dtos = guardianships
                .Where(g => playersById.ContainsKey(g.PlayerId))
                .Select(g =>
                {
                    var player = playersById[g.PlayerId];
                    return new MyChildDto(
                        player.Id, player.FirstName, player.LastName, player.BirthDate,
                        player.DefaultPosition.ToString(), player.PhotoUrl,
                        g.RelationshipType.ToString(), g.CanEdit);
                })
                .ToList();

            return Result.Success<IReadOnlyList<MyChildDto>>(dtos);
        }
    }
}
