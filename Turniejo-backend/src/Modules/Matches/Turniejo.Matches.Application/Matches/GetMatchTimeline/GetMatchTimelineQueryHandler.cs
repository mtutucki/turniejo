using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchTimeline
{
    internal sealed class GetMatchTimelineQueryHandler(
        IMatchesDbContext dbContext,
        ITeamModule teamModule,
        IPlayerModule playerModule) : IQueryHandler<GetMatchTimelineQuery, IReadOnlyList<MatchTimelineEntryDto>>
    {
        public async Task<Result<IReadOnlyList<MatchTimelineEntryDto>>> Handle(GetMatchTimelineQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<IReadOnlyList<MatchTimelineEntryDto>>(Error.NotFound("Match", request.MatchId));

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);

            var teamNames = new Dictionary<Guid, string>();
            foreach (var tournamentTeamId in new[] { match.HomeTournamentTeamId, match.AwayTournamentTeamId })
            {
                var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
                var team = tournamentTeam != null
                    ? await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken)
                    : null;

                teamNames[tournamentTeamId] = team?.Name ?? "Unknown";
            }

            var playerIds = events
                .SelectMany(e => new[] { e.PlayerId, e.RelatedPlayerId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var players = playerIds.Count > 0
                ? await playerModule.GetPlayersAsync(playerIds, cancellationToken)
                : [];
            var playerNames = players.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}");

            var timeline = events
                .OrderBy(e => e.Minute)
                .ThenBy(e => e.Second ?? 0)
                .ThenBy(e => e.CreatedAt)
                .Select(e => new MatchTimelineEntryDto(
                    e.Id,
                    e.Type.ToString(),
                    e.Minute,
                    e.Second,
                    e.TournamentTeamId.HasValue && teamNames.TryGetValue(e.TournamentTeamId.Value, out var teamName) ? teamName : null,
                    e.PlayerId.HasValue && playerNames.TryGetValue(e.PlayerId.Value, out var playerName) ? playerName : null,
                    e.RelatedPlayerId.HasValue && playerNames.TryGetValue(e.RelatedPlayerId.Value, out var relatedName) ? relatedName : null))
                .ToList();

            return Result.Success<IReadOnlyList<MatchTimelineEntryDto>>(timeline);
        }
    }
}
