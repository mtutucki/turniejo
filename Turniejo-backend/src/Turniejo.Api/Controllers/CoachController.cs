using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Players.Application.TournamentPlayers.AssignPlayerToTournamentTeam;
using Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers;
using Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer;
using Turniejo.Players.Domain.Enums;
using Turniejo.Teams.Application.TournamentTeams.GetMyTeams;
using Turniejo.Teams.Application.TournamentTeams.GetTeamPlayerStats;
using Turniejo.Teams.Application.TournamentTeams.GetTeamResults;
using Turniejo.Teams.Application.TournamentTeams.GetTeamSchedule;

namespace Turniejo.Api.Controllers
{
    [ApiController]
    public sealed class CoachController(ISender sender) : ControllerBase
    {
        [HttpGet("/api/coach/teams")]
        public async Task<IActionResult> GetMyTeams(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyTeamsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/coach/teams/{id}/players")]
        public async Task<IActionResult> GetTeamPlayers([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            // TournamentId nieużywane przez handler (filtrowanie tylko po TournamentTeamId) - autoryzacja
            // trenera przechodzi przez AlternateAuthorizedScope po TournamentTeamId, więc Guid.Empty jest bezpieczne.
            var result = await sender.Send(new GetTournamentTeamPlayersQuery(Guid.Empty, id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/coach/teams/{id}/schedule")]
        public async Task<IActionResult> GetTeamSchedule([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTeamScheduleQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/coach/teams/{id}/results")]
        public async Task<IActionResult> GetTeamResults([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTeamResultsQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/coach/teams/{id}/player-stats")]
        public async Task<IActionResult> GetTeamPlayerStats([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTeamPlayerStatsQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("/api/tournament-players/{id}")]
        public async Task<IActionResult> UpdateTournamentPlayer([FromRoute] Guid id, [FromBody] UpdateTournamentPlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentPlayerCommand(request.TournamentId, request.TournamentTeamId, id, request.ShirtNumber, request.Position, request.Status);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("/api/tournament-teams/{id}/players")]
        public async Task<IActionResult> AssignPlayer([FromRoute] Guid id, [FromBody] AssignPlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignPlayerToTournamentTeamCommand(request.TournamentId, id, request.PlayerId, request.ShirtNumber, request.Position);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournament-players/{result.Value.TournamentPlayerId}", result.Value);
        }

        public sealed record UpdateTournamentPlayerRequest(Guid TournamentId, Guid TournamentTeamId, int? ShirtNumber, PlayerPosition? Position, TournamentPlayerStatus Status);
        public sealed record AssignPlayerRequest(Guid TournamentId, Guid PlayerId, int? ShirtNumber, PlayerPosition? Position);
    }
}
