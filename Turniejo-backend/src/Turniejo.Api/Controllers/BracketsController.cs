using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Brackets.Application.Brackets.GenerateBracket;
using Turniejo.Brackets.Application.Brackets.GetBracket;
using Turniejo.Brackets.Application.Brackets.ResetBracket;
using Turniejo.Brackets.Application.Brackets.SwapBracketSlots;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Api.Controllers
{
    [Route("api/tournaments/{tournamentId}/bracket")]
    [ApiController]
    public sealed class BracketsController(ISender sender) : ControllerBase
    {
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateBracket([FromRoute] Guid tournamentId, [FromBody] GenerateBracketRequest request, CancellationToken cancellationToken)
        {
            var pairings = request.RoundOnePairings
                .Select(p => new BracketPairingInput(
                    p.HomeSourceType, p.HomeGroupId, p.HomePosition, p.HomeTournamentTeamId,
                    p.AwaySourceType, p.AwayGroupId, p.AwayPosition, p.AwayTournamentTeamId))
                .ToList();

            var command = new GenerateBracketCommand(tournamentId, request.CategoryId, pairings);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetBracket([FromRoute] Guid tournamentId, [FromQuery] Guid categoryId, CancellationToken cancellationToken)
        {
            var query = new GetBracketQuery(tournamentId, categoryId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete]
        public async Task<IActionResult> ResetBracket([FromRoute] Guid tournamentId, [FromQuery] Guid categoryId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ResetBracketCommand(tournamentId, categoryId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("swap-slots")]
        public async Task<IActionResult> SwapBracketSlots([FromRoute] Guid tournamentId, [FromBody] SwapBracketSlotsRequest request, CancellationToken cancellationToken)
        {
            var command = new SwapBracketSlotsCommand(
                tournamentId, request.CategoryId,
                request.FirstNodeId, request.FirstSlot, request.SecondNodeId, request.SecondSlot);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record SwapBracketSlotsRequest(
            Guid CategoryId, Guid FirstNodeId, BracketSlot FirstSlot, Guid SecondNodeId, BracketSlot SecondSlot);

        public sealed record BracketPairingRequest(
            BracketSourceType HomeSourceType, Guid? HomeGroupId, int? HomePosition, Guid? HomeTournamentTeamId,
            BracketSourceType AwaySourceType, Guid? AwayGroupId, int? AwayPosition, Guid? AwayTournamentTeamId);

        public sealed record GenerateBracketRequest(Guid CategoryId, IReadOnlyList<BracketPairingRequest> RoundOnePairings);
    }
}
