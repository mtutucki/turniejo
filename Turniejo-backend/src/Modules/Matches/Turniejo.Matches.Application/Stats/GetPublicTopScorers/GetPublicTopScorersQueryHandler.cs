using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Stats.GetPublicTopScorers
{
    internal sealed class GetPublicTopScorersQueryHandler(
        IMatchesDbContext dbContext,
        IPlayerModule playerModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicTopScorersQuery, IReadOnlyList<PlayerRankingEntryDto>>
    {
        // OwnGoal celowo pominięty — to gol wliczany do wyniku przeciwnej drużyny, więc nie może
        // zasilać rankingu strzelców sprawcy (patrz GetTopScorersQueryHandler, ta sama zasada).
        private static readonly MatchEventType[] GoalTypes = [MatchEventType.Goal, MatchEventType.PenaltyGoal];

        public async Task<Result<IReadOnlyList<PlayerRankingEntryDto>>> Handle(GetPublicTopScorersQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<PlayerRankingEntryDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowStats)
                return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>([]);

            var events = await dbContext.GetMatchEventsByTournamentIdAndTypesAsync(tournament.Id, GoalTypes, cancellationToken);

            var goalCounts = events
                .Where(e => e.PlayerId.HasValue)
                .GroupBy(e => e.PlayerId!.Value)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PlayerId)
                .Take(request.Top)
                .ToList();

            var players = await playerModule.GetPlayersAsync(goalCounts.Select(x => x.PlayerId).ToList(), cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var ranking = goalCounts
                .Select(x => playersById.TryGetValue(x.PlayerId, out var player)
                    ? new PlayerRankingEntryDto(x.PlayerId, player.FirstName, player.LastName, x.Count)
                    : new PlayerRankingEntryDto(x.PlayerId, "Unknown", "Unknown", x.Count))
                .ToList();

            return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>(ranking);
        }
    }
}
