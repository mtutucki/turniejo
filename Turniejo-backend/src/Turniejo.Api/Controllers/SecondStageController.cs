using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Brackets.Application.SecondStage.GetSecondStage;
using Turniejo.Brackets.Application.SecondStage.SetSecondStageRanges;
using Turniejo.Brackets.Application.SecondStage.SetSecondStageSeats;

namespace Turniejo.Api.Controllers
{
    /// <summary>Format „Dwie fazy grupowe”: które miejsca z tabel fazy 1 trafiają do której grupy fazy 2.</summary>
    [Route("api/tournaments/{tournamentId}/second-stage")]
    [ApiController]
    public sealed class SecondStageController(ISender sender) : ControllerBase
    {
        /// <summary>Podział na grupy fazy 2 i zaplanowane mecze (terminarz pokazuje je, zanim znane są drużyny).</summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetSecondStageQuery(tournamentId), cancellationToken);
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

        /// <summary>Zakresy miejsc dla grup fazy 2 (np. Grupa I: 1-2). Działa, zanim grupy mają drużyny.</summary>
        [HttpPut("ranges")]
        public async Task<IActionResult> SetRanges([FromRoute] Guid tournamentId, [FromBody] SetSecondStageRangesRequest request, CancellationToken cancellationToken)
        {
            var ranges = request.Ranges
                .Select(r => new SecondStageRangeInput(r.GroupId, r.FromPosition, r.ToPosition))
                .ToList();
            var result = await sender.Send(new SetSecondStageRangesCommand(tournamentId, request.CategoryId, ranges), cancellationToken);
            return result.ToActionResult();
        }

        public sealed record SecondStageRangeRequest(Guid GroupId, int FromPosition, int ToPosition);
        public sealed record SetSecondStageRangesRequest(Guid CategoryId, IReadOnlyList<SecondStageRangeRequest> Ranges);
        public sealed record SecondStageSeatRequest(Guid GroupId, Guid SourceGroupId, int SourcePosition);
        public sealed record SetSecondStageSeatsRequest(Guid CategoryId, IReadOnlyList<SecondStageSeatRequest> Seats);
    }
}
