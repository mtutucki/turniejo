using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Teams.Application.TeamLabels.CreateTeamLabel;
using Turniejo.Teams.Application.TeamLabels.DeleteTeamLabel;
using Turniejo.Teams.Application.TeamLabels.GetTeamLabels;
using Turniejo.Teams.Application.TeamLabels.RenameTeamLabel;
using Turniejo.Teams.Application.TeamLabels.SetTeamLabelMembers;

namespace Turniejo.Api.Controllers
{
    /// <summary>Etykiety drużyn w bazie organizacji (np. rocznik, klub). Tylko organizator organizacji.</summary>
    [Route("api/organizations/{organizationId}/team-labels")]
    [ApiController]
    public sealed class TeamLabelsController(ISender sender) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetTeamLabels([FromRoute] Guid organizationId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTeamLabelsQuery(organizationId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeamLabel([FromRoute] Guid organizationId, [FromBody] TeamLabelRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new CreateTeamLabelCommand(organizationId, request.Name ?? string.Empty), cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/organizations/{organizationId}/team-labels/{result.Value.LabelId}", result.Value);
        }

        [HttpPut("{labelId}")]
        public async Task<IActionResult> RenameTeamLabel([FromRoute] Guid organizationId, [FromRoute] Guid labelId, [FromBody] TeamLabelRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RenameTeamLabelCommand(organizationId, labelId, request.Name ?? string.Empty), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{labelId}")]
        public async Task<IActionResult> DeleteTeamLabel([FromRoute] Guid organizationId, [FromRoute] Guid labelId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeleteTeamLabelCommand(organizationId, labelId), cancellationToken);
            return result.ToActionResult();
        }

        // Drużyny z etykietą: te z listy ją mają, pozostałe tracą.
        [HttpPut("{labelId}/teams")]
        public async Task<IActionResult> SetTeamLabelMembers([FromRoute] Guid organizationId, [FromRoute] Guid labelId, [FromBody] TeamLabelMembersRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new SetTeamLabelMembersCommand(organizationId, labelId, request.TeamIds ?? []), cancellationToken);
            return result.ToActionResult();
        }

        public sealed record TeamLabelRequest(string? Name);
        public sealed record TeamLabelMembersRequest(IReadOnlyCollection<Guid>? TeamIds);
    }
}
