using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Helpers;
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
            // Zakończony mecz też ma stronę na portalu (strzelcy, kartki) — wtedy wystarczy widoczny terminarz;
            // trwający wymaga ShowLiveMatches. Zaplanowany nie ma jeszcze czego pokazać.
            if (match == null || match.Status is not (MatchStatus.Live or MatchStatus.Paused or MatchStatus.Finished))
                return Result.Failure<MatchLivePanelDto>(Error.NotFound("Match", request.MatchId));

            var settings = await tournamentModule.GetTournamentPublicSettingsAsync(match.TournamentId, cancellationToken);
            var visible = match.Status == MatchStatus.Finished
                ? settings is { IsPublic: true } && (settings.ShowSchedule || settings.ShowLiveMatches)
                : settings is { IsPublic: true, ShowLiveMatches: true };
            if (!visible || settings is null)
                return Result.Failure<MatchLivePanelDto>(Error.NotFound("Match", request.MatchId));

            // ShowLiveMatches włącza sam podgląd meczu — ShowTeams/ShowPlayers rządzą OSOBNO tym, czy
            // wolno ujawnić tożsamość drużyn i pełne składy (imiona, numery, pozycje zawodników).
            // Wcześniej sprawdzane było wyłącznie ShowLiveMatches, więc organizator wyłączający
            // ShowPlayers wciąż publicznie ujawniał pełne składy na żywo.
            var homeTeam = await BuildTeamDtoAsync(
                match.HomeTournamentTeamId, "Drużyna gospodarzy", settings.ShowTeams, settings.ShowPlayers, settings.AbbreviatePlayerNames, cancellationToken);
            var awayTeam = await BuildTeamDtoAsync(
                match.AwayTournamentTeamId, "Drużyna gości", settings.ShowTeams, settings.ShowPlayers, settings.AbbreviatePlayerNames, cancellationToken);

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var eventDtos = events
                .Select(e => new MatchEventDto(
                    e.Id, e.MatchId, e.Type.ToString(), e.TournamentTeamId, e.PlayerId, e.RelatedPlayerId,
                    // CreatedByUserId to id konta sędziego/organizatora — nie ujawniamy go anonimowym widzom.
                    e.Minute, e.Second, Guid.Empty, e.CreatedAt))
                .ToList();

            var dto = new MatchLivePanelDto(
                match.Id, match.TournamentId, match.Status.ToString(), match.StageType.ToString(), match.ScheduledAt,
                match.StartedAt, match.PausedAt, match.FinishedAt, match.TotalPausedSeconds,
                match.HomeScore, match.AwayScore, match.ResultType.ToString(), match.WinnerTournamentTeamId,
                homeTeam, awayTeam, eventDtos, null,
                match.GetElapsedSeconds(match.FinishedAt ?? match.PausedAt ?? DateTimeOffset.UtcNow),
                match.HomePenaltyScore, match.AwayPenaltyScore);

            return Result.Success(dto);
        }

        private async Task<MatchTeamDto> BuildTeamDtoAsync(
            Guid tournamentTeamId, string fallbackName, bool showTeams, bool showPlayers, bool abbreviateNames, CancellationToken cancellationToken)
        {
            var playerDtos = new List<MatchPlayerDto>();
            if (showPlayers)
            {
                var players = await playerModule.GetTournamentTeamPlayersAsync(tournamentTeamId, cancellationToken);
                playerDtos = players
                    .Select(p => new MatchPlayerDto(p.Id, p.PlayerId, p.FirstName, PublicPlayerName.LastName(p.LastName, abbreviateNames), p.ShirtNumber, p.Position, p.Status))
                    .ToList();
            }

            if (!showTeams)
                return new MatchTeamDto(tournamentTeamId, Guid.Empty, fallbackName, null, playerDtos);

            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
            var team = tournamentTeam != null
                ? await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken)
                : null;

            return new MatchTeamDto(
                tournamentTeamId,
                team?.Id ?? Guid.Empty,
                team?.Name ?? "Unknown",
                team?.ShortName,
                playerDtos);
        }
    }
}
