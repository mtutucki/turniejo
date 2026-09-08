using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildren;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildResults;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildSchedule;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildStanding;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildTeam;
using Turniejo.Players.Application.Players.UpdatePlayer;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Api.Controllers
{
    [Route("api/parent")]
    [ApiController]
    public sealed class ParentController(ISender sender) : ControllerBase
    {
        [HttpGet("players")]
        public async Task<IActionResult> GetMyChildren(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyChildrenQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("players/{id}/team")]
        public async Task<IActionResult> GetMyChildTeam([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyChildTeamQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("players/{id}/schedule")]
        public async Task<IActionResult> GetMyChildSchedule([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyChildScheduleQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("players/{id}/results")]
        public async Task<IActionResult> GetMyChildResults([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyChildResultsQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("players/{id}/standing")]
        public async Task<IActionResult> GetMyChildStanding([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyChildStandingQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("players/{id}")]
        public async Task<IActionResult> UpdatePlayer([FromRoute] Guid id, [FromBody] UpdateMyChildRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdatePlayerCommand(id, request.FirstName, request.LastName, request.BirthDate, request.DefaultPosition, request.PhotoUrl);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record UpdateMyChildRequest(string FirstName, string LastName, DateTime BirthDate, PlayerPosition DefaultPosition, string? PhotoUrl);
    }
}
