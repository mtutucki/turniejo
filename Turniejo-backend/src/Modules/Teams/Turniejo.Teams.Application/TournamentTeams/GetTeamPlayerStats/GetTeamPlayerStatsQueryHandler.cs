using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamPlayerStats
{
    internal sealed class GetTeamPlayerStatsQueryHandler(
        IMatchModule matchModule,
        IPlayerModule playerModule) : IQueryHandler<GetTeamPlayerStatsQuery, IReadOnlyList<TeamPlayerStatsDto>>
    {
        public async Task<Result<IReadOnlyList<TeamPlayerStatsDto>>> Handle(GetTeamPlayerStatsQuery request, CancellationToken cancellationToken)
        {
            var roster = await playerModule.GetTournamentTeamPlayersAsync(request.TournamentTeamId, cancellationToken);
            var events = await matchModule.GetMatchEventsByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken);

            var dtos = roster.Select(player => new TeamPlayerStatsDto(
                player.Id, player.PlayerId, player.FirstName, player.LastName,
                Goals: events.Count(e => e.PlayerId == player.PlayerId && e.Type is "Goal" or "OwnGoal" or "PenaltyGoal"),
                Assists: events.Count(e =>
                    (e.Type == "Assist" && e.PlayerId == player.PlayerId) ||
                    (e.Type is "Goal" or "PenaltyGoal" && e.RelatedPlayerId == player.PlayerId)),
                YellowCards: events.Count(e => e.PlayerId == player.PlayerId && e.Type == "YellowCard"),
                RedCards: events.Count(e => e.PlayerId == player.PlayerId && e.Type == "RedCard")))
                .ToList();

            return Result.Success<IReadOnlyList<TeamPlayerStatsDto>>(dtos);
        }
    }
}
