using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentById;
using Turniejo.Tournaments.Application.Tournaments.GetTournaments;
using Turniejo.Tournaments.Application.Tournaments.JoinTournament;
using Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournament;

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
                return result.ToActionResult();

            return Created($"api/tournaments/{result.Value.TournamentId}", result.Value);
        }

        [HttpPost("join")]
        public async Task<IActionResult> Join([FromBody] JoinTournamentCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetTournamentsByOrganizationId([FromQuery] GetTournamentsQuery query, CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}")]
        public async Task<IActionResult> GetTournamentById([FromRoute] GetTournamentByIdQuery query, CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/status")]
        public async Task<IActionResult> ChangeTournamentStatus([FromRoute] Guid tournamentId, [FromBody] ChangeTournamentStatusCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/format")]
        public async Task<IActionResult> UpdateTournamentFormat([FromRoute] Guid tournamentId, [FromBody] SetTournamentFormatCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}")]
        public async Task<IActionResult> UpdateTournamentDetails([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentDetailsCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }
    }
}