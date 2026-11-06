using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Players.Application.TeamPlayers.CreateTeamPlayer;
using Turniejo.Players.Application.TeamPlayers.GetTeamRoster;
using Turniejo.Players.Application.TeamPlayers.RemoveTeamPlayer;
using Turniejo.Players.Application.TeamPlayers.RestoreTeamPlayer;
using Turniejo.Players.Application.TeamPlayers.UpdateTeamPlayer;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Api.Controllers
{
    // Kadra drużyny z bazy. Bez organizationId w adresie, bo trener zna tylko drużynę, nie organizację.
    // Dostęp sprawdzają handlery (TeamRosterAccessGuard).
    [Route("api/teams/{teamId}/players")]
    [ApiController]
    public sealed class TeamRosterController(ISender sender) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetRoster([FromRoute] Guid teamId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTeamRosterQuery(teamId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlayer([FromRoute] Guid teamId, [FromBody] TeamPlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateTeamPlayerCommand(
                teamId, request.FirstName, request.LastName, request.BirthDate, request.DefaultPosition,
                request.ShirtNumber, request.AllowDuplicate);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/teams/{teamId}/players/{result.Value.PlayerId}", result.Value);
        }

        [HttpPut("{playerId}")]
        public async Task<IActionResult> UpdatePlayer([FromRoute] Guid teamId, [FromRoute] Guid playerId, [FromBody] TeamPlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTeamPlayerCommand(
                teamId, playerId, request.FirstName, request.LastName, request.BirthDate, request.DefaultPosition, request.ShirtNumber);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{playerId}")]
        public async Task<IActionResult> RemovePlayer(
            [FromRoute] Guid teamId, [FromRoute] Guid playerId, [FromQuery] bool removeFromCurrentSquads, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RemoveTeamPlayerCommand(teamId, playerId, removeFromCurrentSquads), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{playerId}/restore")]
        public async Task<IActionResult> RestorePlayer([FromRoute] Guid teamId, [FromRoute] Guid playerId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RestoreTeamPlayerCommand(teamId, playerId), cancellationToken);
            return result.ToActionResult();
        }

        public sealed record TeamPlayerRequest(
            string FirstName,
            string LastName,
            DateTime BirthDate,
            PlayerPosition DefaultPosition,
            int? ShirtNumber,
            bool AllowDuplicate = false);
    }
}
