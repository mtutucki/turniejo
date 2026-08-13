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
using Turniejo.Tournaments.Domain.Enums;

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
        public async Task<IActionResult> ChangeTournamentStatus([FromRoute] Guid tournamentId, [FromBody] ChangeTournamentStatusRequest request, CancellationToken cancellationToken)
        {
            var command = new ChangeTournamentStatusCommand(tournamentId, request.TournamentStatus);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/format")]
        public async Task<IActionResult> UpdateTournamentFormat([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentFormatRequest request, CancellationToken cancellationToken)
        {
            var command = new SetTournamentFormatCommand(tournamentId, request.TournamentFormat);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}")]
        public async Task<IActionResult> UpdateTournamentDetails([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentDetailsRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentDetailsCommand(tournamentId, request.Name, request.StartAt, request.EndAt, request.TeamLimit, request.Description);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record ChangeTournamentStatusRequest(TournamentStatus TournamentStatus);
        public sealed record UpdateTournamentFormatRequest(TournamentFormat TournamentFormat);
        public sealed record UpdateTournamentDetailsRequest(string Name, DateTimeOffset StartAt, DateTimeOffset EndAt, int TeamLimit, string? Description);

    }
}