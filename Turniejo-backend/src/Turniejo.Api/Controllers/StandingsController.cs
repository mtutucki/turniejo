using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Standings.Application.Standings.GetStandings;

namespace Turniejo.Api.Controllers
{
    [Route("api/tournaments/{tournamentId}/standings")]
    [ApiController]
    public sealed class StandingsController(ISender sender) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetStandings([FromRoute] Guid tournamentId, [FromQuery] Guid groupId, CancellationToken cancellationToken)
        {
            var query = new GetStandingsQuery(tournamentId, groupId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }
    }
}
