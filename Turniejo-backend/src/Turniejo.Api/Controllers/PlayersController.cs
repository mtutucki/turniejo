using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Players.Application.Players.AssignUserToPlayer;
using Turniejo.Players.Application.Players.CreatePlayer;
using Turniejo.Players.Application.Players.GetPlayerById;
using Turniejo.Players.Application.Players.UpdatePlayer;
using Turniejo.Players.Application.PlayerGuardians.AssignGuardianToPlayer;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildren;
using Turniejo.Players.Application.PlayerGuardians.GetPlayerGuardians;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Api.Controllers
{
    [Route("api/players")]
    [ApiController]
    public sealed class PlayersController(ISender sender) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new CreatePlayerCommand(request.FirstName, request.LastName, request.BirthDate, request.DefaultPosition, request.PhotoUrl, request.UserId);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/players/{result.Value.PlayerId}", result.Value);
        }

        [HttpPut("{playerId}")]
        public async Task<IActionResult> UpdatePlayer([FromRoute] Guid playerId, [FromBody] UpdatePlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdatePlayerCommand(playerId, request.FirstName, request.LastName, request.BirthDate, request.DefaultPosition, request.PhotoUrl);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("my-children")]
        public async Task<IActionResult> GetMyChildren(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyChildrenQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{playerId}")]
        public async Task<IActionResult> GetPlayerById([FromRoute] Guid playerId, CancellationToken cancellationToken)
        {
            var query = new GetPlayerByIdQuery(playerId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{playerId}/link-user")]
        public async Task<IActionResult> LinkUser([FromRoute] Guid playerId, [FromBody] LinkUserRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignUserToPlayerCommand(playerId, request.UserId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{playerId}/guardians")]
        public async Task<IActionResult> AssignGuardian([FromRoute] Guid playerId, [FromBody] AssignGuardianRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignGuardianToPlayerCommand(request.TournamentId, request.TournamentTeamId, playerId, request.UserId, request.RelationshipType, request.CanEdit, request.CanReceiveNotifications);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/players/{playerId}/guardians/{result.Value.PlayerGuardianId}", result.Value);
        }

        [HttpGet("{playerId}/guardians")]
        public async Task<IActionResult> GetPlayerGuardians([FromRoute] Guid playerId, CancellationToken cancellationToken)
        {
            var query = new GetPlayerGuardiansQuery(playerId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record CreatePlayerRequest(string FirstName, string LastName, DateTime BirthDate, PlayerPosition DefaultPosition, string? PhotoUrl, Guid? UserId);
        public sealed record UpdatePlayerRequest(string FirstName, string LastName, DateTime BirthDate, PlayerPosition DefaultPosition, string? PhotoUrl);
        public sealed record LinkUserRequest(Guid UserId);
        public sealed record AssignGuardianRequest(Guid TournamentId, Guid TournamentTeamId, Guid UserId, RelationshipType RelationshipType, bool CanEdit, bool CanReceiveNotifications);
    }
}
