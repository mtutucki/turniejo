using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Helpers;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Stats.GetPublicTopAssists
{
    internal sealed class GetPublicTopAssistsQueryHandler(
        IMatchesDbContext dbContext,
        IPlayerModule playerModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicTopAssistsQuery, IReadOnlyList<PlayerRankingEntryDto>>
    {
        private static readonly MatchEventType[] AssistRelevantTypes = [MatchEventType.Goal, MatchEventType.PenaltyGoal];

        public async Task<Result<IReadOnlyList<PlayerRankingEntryDto>>> Handle(GetPublicTopAssistsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<PlayerRankingEntryDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowStats)
                return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>([]);

            var events = await dbContext.GetMatchEventsByTournamentIdAndTypesAsync(tournament.Id, AssistRelevantTypes, cancellationToken);

            var assisterIds = events
                .Where(e => e.RelatedPlayerId.HasValue)
                .Select(e => (PlayerId: e.RelatedPlayerId!.Value, TeamId: e.TournamentTeamId));

            var assistCounts = assisterIds
                .GroupBy(x => x.PlayerId)
                .Select(g => new { PlayerId = g.Key, Count = g.Count(), TeamId = g.Select(x => x.TeamId).FirstOrDefault(t => t.HasValue) })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PlayerId)
                .Take(request.Top)
                .ToList();

            var players = await playerModule.GetPlayersAsync(assistCounts.Select(x => x.PlayerId).ToList(), cancellationToken);
            var playersById = players.ToDictionary(p => p.Id);

            var ranking = assistCounts
                .Select(x => playersById.TryGetValue(x.PlayerId, out var player)
                    ? new PlayerRankingEntryDto(x.PlayerId, player.FirstName,
                        PublicPlayerName.LastName(player.LastName, tournament.Settings.AbbreviatePlayerNames), x.Count, x.TeamId)
                    : new PlayerRankingEntryDto(x.PlayerId, "Unknown", "Unknown", x.Count, x.TeamId))
                .ToList();

            return Result.Success<IReadOnlyList<PlayerRankingEntryDto>>(ranking);
        }
    }
}
