using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Stats.GetTopScorers
{
    internal sealed class GetTopScorersQueryHandler(
        IMatchesDbContext dbContext,
        IPlayerModule playerModule) : IQueryHandler<GetTopScorersQuery, IReadOnlyList<PlayerRankingEntryDto>>
    {
        // OwnGoal celowo pominięty — to gol wliczany do wyniku przeciwnej drużyny (patrz
        // RecalculateMatchScoreCommandHandler), więc nie może zasilać rankingu strzelców sprawcy.
        private static readonly MatchEventType[] GoalTypes =
        [
            MatchEventType.Goal,
            MatchEventType.PenaltyGoal
        ];

        public async Task<Result<IReadOnlyList<PlayerRankingEntryDto>>> Handle(GetTopScorersQuery request, CancellationToken cancellationToken)
        {
            var events = await dbContext.GetMatchEventsByTournamentIdAndTypesAsync(request.TournamentId, GoalTypes, cancellationToken);

            var goalCounts = events
                .Where(e => e.PlayerId.HasValue)
                .GroupBy(e => e.PlayerId!.Value)
                .Select(g => new { PlayerId = g.Key, Count = g.Count(), TeamId = g.Select(e => e.TournamentTeamId).FirstOrDefault(t => t.HasValue) })
                .OrderByDescending(x => x.Count)
                // ThenBy deterministyczny — bez drugiego klucza kolejność remisujących na granicy Top
                // zależałaby od niegwarantowanej kolejności materializacji z bazy.
                .ThenBy(x => x.PlayerId)
                .Take(request.Top)
                .ToList();

            var players = await playerModule.GetPlayersAsync(goalCounts.Select(x => x.PlayerId).ToList(), cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var ranking = goalCounts
                .Select(x => playersById.TryGetValue(x.PlayerId, out var player)
                    ? new PlayerRankingEntryDto(x.PlayerId, player.FirstName, player.LastName, x.Count, x.TeamId)
                    : new PlayerRankingEntryDto(x.PlayerId, "Unknown", "Unknown", x.Count, x.TeamId))
                .ToList();

            return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>(ranking);
        }
    }
}
