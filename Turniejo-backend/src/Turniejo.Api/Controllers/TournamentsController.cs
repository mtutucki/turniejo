using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;

namespace Turniejo.Api.Controllers
{
    [Route("api/tournaments")]
    [ApiController]
    public sealed class TournamentsController(ISender sender) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTournamentCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Created($"api/tournaments/{result.Value.TournamentId}", result.Value);
        }
    }
}
