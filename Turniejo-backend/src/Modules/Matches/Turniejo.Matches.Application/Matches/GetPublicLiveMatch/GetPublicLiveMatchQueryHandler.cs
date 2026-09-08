using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetPublicLiveMatch
{
    internal sealed class GetPublicLiveMatchQueryHandler(
        IMatchesDbContext dbContext,
        ITeamModule teamModule,
        IPlayerModule playerModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicLiveMatchQuery, MatchLivePanelDto>
    {
        public async Task<Result<MatchLivePanelDto>> Handle(GetPublicLiveMatchQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.Status is not (MatchStatus.Live or MatchStatus.Paused))
                return Result.Failure<MatchLivePanelDto>(Error.NotFound("Match", request.MatchId));

            var settings = await tournamentModule.GetTournamentPublicSettingsAsync(match.TournamentId, cancellationToken);
            if (settings == null || !settings.IsPublic || !settings.ShowLiveMatches)
                return Result.Failure<MatchLivePanelDto>(Error.NotFound("Match", request.MatchId));

            var homeTeam = await BuildTeamDtoAsync(match.HomeTournamentTeamId, cancellationToken);
            var awayTeam = await BuildTeamDtoAsync(match.AwayTournamentTeamId, cancellationToken);

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var eventDtos = events
                .Select(e => new MatchEventDto(
                    e.Id, e.MatchId, e.Type.ToString(), e.TournamentTeamId, e.PlayerId, e.RelatedPlayerId,
                    e.Minute, e.Second, e.CreatedByUserId, e.CreatedAt))
                .ToList();

            var dto = new MatchLivePanelDto(
                match.Id, match.TournamentId, match.Status.ToString(), match.StageType.ToString(), match.ScheduledAt,
                match.StartedAt, match.PausedAt, match.FinishedAt, match.TotalPausedSeconds,
                match.HomeScore, match.AwayScore, match.ResultType.ToString(), match.WinnerTournamentTeamId,
                homeTeam, awayTeam, eventDtos);

            return Result.Success(dto);
        }

        private async Task<MatchTeamDto> BuildTeamDtoAsync(Guid tournamentTeamId, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
            var team = tournamentTeam != null
                ? await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken)
                : null;
            var players = await playerModule.GetTournamentTeamPlayersAsync(tournamentTeamId, cancellationToken);

            var playerDtos = players
                .Select(p => new MatchPlayerDto(p.Id, p.PlayerId, p.FirstName, p.LastName, p.ShirtNumber, p.Position, p.Status))
                .ToList();

            return new MatchTeamDto(
                tournamentTeamId,
                team?.Id ?? Guid.Empty,
                team?.Name ?? "Unknown",
                team?.ShortName,
                playerDtos);
        }
    }
}
