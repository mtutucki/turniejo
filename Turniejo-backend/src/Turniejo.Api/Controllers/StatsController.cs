using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Matches.Application.Stats.GetMvpRanking;
using Turniejo.Matches.Application.Stats.GetTopAssists;
using Turniejo.Matches.Application.Stats.GetTopScorers;

namespace Turniejo.Api.Controllers
{
    [Route("api/tournaments/{tournamentId}/stats")]
    [ApiController]
    public sealed class StatsController(ISender sender) : ControllerBase
    {
        [HttpGet("top-scorers")]
        public async Task<IActionResult> GetTopScorers([FromRoute] Guid tournamentId, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        {
            var query = new GetTopScorersQuery(tournamentId, top);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("top-assists")]
        public async Task<IActionResult> GetTopAssists([FromRoute] Guid tournamentId, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        {
            var query = new GetTopAssistsQuery(tournamentId, top);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("mvp")]
        public async Task<IActionResult> GetMvpRanking([FromRoute] Guid tournamentId, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        {
            var query = new GetMvpRankingQuery(tournamentId, top);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }
    }
}
