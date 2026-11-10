using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Players.Application.TeamPlayers.GetTeamHistory;

namespace Turniejo.Api.Controllers
{
    // Statystyki drużyny z bazy we wszystkich turniejach. Dostęp jak do kadry (TeamRosterAccessGuard).
    [Route("api/teams/{teamId}/history")]
    [ApiController]
    public sealed class TeamHistoryController(ISender sender) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetHistory([FromRoute] Guid teamId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTeamHistoryQuery(teamId), cancellationToken);
            return result.ToActionResult();
        }
    }
}
