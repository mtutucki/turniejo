using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Stats.GetTopAssists
{
    internal sealed class GetTopAssistsQueryHandler(
        IMatchesDbContext dbContext,
        IPlayerModule playerModule) : IQueryHandler<GetTopAssistsQuery, IReadOnlyList<PlayerRankingEntryDto>>
    {
        // Assist ma jedną ścieżkę zapisu: RelatedPlayerId gola/rzutu karnego (AddGoalCommand przy
        // strzale, AddAssistCommand dopisując go po fakcie do istniejącego gola). Osobny typ
        // MatchEventType.Assist już nie jest nigdzie tworzony — liczenie go tutaj podwajałoby wynik.
        private static readonly MatchEventType[] AssistRelevantTypes =
        [
            MatchEventType.Goal,
            MatchEventType.PenaltyGoal
        ];

        public async Task<Result<IReadOnlyList<PlayerRankingEntryDto>>> Handle(GetTopAssistsQuery request, CancellationToken cancellationToken)
        {
            var events = await dbContext.GetMatchEventsByTournamentIdAndTypesAsync(request.TournamentId, AssistRelevantTypes, cancellationToken);

            var assisterIds = events
                .Where(e => e.RelatedPlayerId.HasValue)
                .Select(e => e.RelatedPlayerId!.Value);

            var assistCounts = assisterIds
                .GroupBy(id => id)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PlayerId)
                .Take(request.Top)
                .ToList();

            var players = await playerModule.GetPlayersAsync(assistCounts.Select(x => x.PlayerId).ToList(), cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var ranking = assistCounts
                .Select(x => playersById.TryGetValue(x.PlayerId, out var player)
                    ? new PlayerRankingEntryDto(x.PlayerId, player.FirstName, player.LastName, x.Count)
                    : new PlayerRankingEntryDto(x.PlayerId, "Unknown", "Unknown", x.Count))
                .ToList();

            return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>(ranking);
        }
    }
}
