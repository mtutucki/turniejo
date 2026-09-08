using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers
{
    internal sealed class GetTournamentTeamPlayersQueryHandler(IPlayersDbContext dbContext) : IQueryHandler<GetTournamentTeamPlayersQuery, IReadOnlyList<TournamentPlayerDto>>
    {
        public async Task<Result<IReadOnlyList<TournamentPlayerDto>>> Handle(GetTournamentTeamPlayersQuery request, CancellationToken cancellationToken)
        {
            var players = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken);

            var dtos = players.Select(p => new TournamentPlayerDto(
                p.Id, p.TournamentTeamId, p.PlayerId, p.ShirtNumber,
                p.Position?.ToString(), p.Status.ToString())).ToList();

            return Result.Success<IReadOnlyList<TournamentPlayerDto>>(dtos);
        }
    }
}
