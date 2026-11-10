using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Brackets.Application.SecondStage.GetSecondStageSeats;
using Turniejo.Brackets.Application.SecondStage.SetSecondStageSeats;

namespace Turniejo.Api.Controllers
{
    /// <summary>Format „Dwie fazy grupowe”: które miejsca z tabel fazy 1 trafiają do której grupy fazy 2.</summary>
    [Route("api/tournaments/{tournamentId}/second-stage")]
    [ApiController]
    public sealed class SecondStageController(ISender sender) : ControllerBase
    {
        [HttpGet("seats")]
        public async Task<IActionResult> GetSeats([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetSecondStageSeatsQuery(tournamentId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("seats")]
        public async Task<IActionResult> SetSeats([FromRoute] Guid tournamentId, [FromBody] SetSecondStageSeatsRequest request, CancellationToken cancellationToken)
        {
            var seats = request.Seats
                .Select(s => new SecondStageSeatInput(s.GroupId, s.SourceGroupId, s.SourcePosition))
                .ToList();
            var result = await sender.Send(new SetSecondStageSeatsCommand(tournamentId, request.CategoryId, seats), cancellationToken);
            return result.ToActionResult();
        }

        public sealed record SecondStageSeatRequest(Guid GroupId, Guid SourceGroupId, int SourcePosition);
        public sealed record SetSecondStageSeatsRequest(Guid CategoryId, IReadOnlyList<SecondStageSeatRequest> Seats);
    }
}
