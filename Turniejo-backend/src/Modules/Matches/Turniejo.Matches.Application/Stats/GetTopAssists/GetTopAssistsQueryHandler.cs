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
        private static readonly MatchEventType[] AssistRelevantTypes =
        [
            MatchEventType.Assist,
            MatchEventType.Goal,
            MatchEventType.PenaltyGoal
        ];

        public async Task<Result<IReadOnlyList<PlayerRankingEntryDto>>> Handle(GetTopAssistsQuery request, CancellationToken cancellationToken)
        {
            var events = await dbContext.GetMatchEventsByTournamentIdAndTypesAsync(request.TournamentId, AssistRelevantTypes, cancellationToken);

            var assisterIds = events
                .Select(e => e.Type switch
                {
                    MatchEventType.Assist => e.PlayerId,
                    MatchEventType.Goal or MatchEventType.PenaltyGoal => e.RelatedPlayerId,
                    _ => null
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value);

            var assistCounts = assisterIds
                .GroupBy(id => id)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
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
