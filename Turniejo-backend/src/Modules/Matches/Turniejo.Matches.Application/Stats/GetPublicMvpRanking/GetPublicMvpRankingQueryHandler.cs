using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Stats.GetPublicMvpRanking
{
    internal sealed class GetPublicMvpRankingQueryHandler(
        IMatchesDbContext dbContext,
        IPlayerModule playerModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicMvpRankingQuery, IReadOnlyList<PlayerRankingEntryDto>>
    {
        private static readonly MatchEventType[] MvpTypes = [MatchEventType.MvpSelected];

        public async Task<Result<IReadOnlyList<PlayerRankingEntryDto>>> Handle(GetPublicMvpRankingQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<PlayerRankingEntryDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowStats)
                return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>([]);

            var events = await dbContext.GetMatchEventsByTournamentIdAndTypesAsync(tournament.Id, MvpTypes, cancellationToken);

            var mvpCounts = events
                .Where(e => e.PlayerId.HasValue)
                .GroupBy(e => e.PlayerId!.Value)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PlayerId)
                .Take(request.Top)
                .ToList();

            var players = await playerModule.GetPlayersAsync(mvpCounts.Select(x => x.PlayerId).ToList(), cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var ranking = mvpCounts
                .Select(x => playersById.TryGetValue(x.PlayerId, out var player)
                    ? new PlayerRankingEntryDto(x.PlayerId, player.FirstName, player.LastName, x.Count)
                    : new PlayerRankingEntryDto(x.PlayerId, "Unknown", "Unknown", x.Count))
                .ToList();

            return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>(ranking);
        }
    }
}
