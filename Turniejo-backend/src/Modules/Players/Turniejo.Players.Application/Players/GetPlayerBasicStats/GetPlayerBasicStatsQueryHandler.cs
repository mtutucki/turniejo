using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.GetPlayerBasicStats
{
    internal sealed class GetPlayerBasicStatsQueryHandler(
        IPlayersDbContext dbContext,
        IMatchModule matchModule,
        PlayerAccessGuard playerAccessGuard,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetPlayerBasicStatsQuery, PlayerBasicStatsDto>
    {
        public async Task<Result<PlayerBasicStatsDto>> Handle(GetPlayerBasicStatsQuery request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<PlayerBasicStatsDto>(Error.NotFound("Player", request.PlayerId));

            if (!await playerAccessGuard.CanViewAsync(player, currentUserContext.UserId, cancellationToken))
                throw new UnauthorizedException();

            // Zawodnik mógł grać w wielu drużynach/turniejach (wiele TournamentPlayer) — statystyki
            // sumujemy po wszystkich, tak jak MyChildMatchResolver robi to dla meczów.
            var tournamentPlayers = await dbContext.GetTournamentPlayersByPlayerIdAsync(request.PlayerId, cancellationToken);
            var tournamentTeamIds = tournamentPlayers.Select(tp => tp.TournamentTeamId).Distinct().ToList();

            var events = new List<TeamMatchEventDto>();
            foreach (var tournamentTeamId in tournamentTeamIds)
            {
                var teamEvents = await matchModule.GetMatchEventsByTournamentTeamIdAsync(tournamentTeamId, cancellationToken);
                events.AddRange(teamEvents);
            }

            var dto = new PlayerBasicStatsDto(
                request.PlayerId,
                Goals: events.Count(e => e.PlayerId == request.PlayerId && e.Type is "Goal" or "PenaltyGoal"),
                Assists: events.Count(e => e.Type is "Goal" or "PenaltyGoal" && e.RelatedPlayerId == request.PlayerId),
                YellowCards: events.Count(e => e.PlayerId == request.PlayerId && e.Type == "YellowCard"),
                RedCards: events.Count(e => e.PlayerId == request.PlayerId && e.Type == "RedCard"));

            return Result.Success(dto);
        }
    }
}
