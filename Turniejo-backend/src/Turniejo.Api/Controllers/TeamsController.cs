using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Turniejo.Api.Extensions;
using Turniejo.Teams.Application.Teams.CreateTeam;
using Turniejo.Teams.Application.Teams.GetTeamById;
using Turniejo.Teams.Application.Teams.GetTeams;
using Turniejo.Teams.Application.Teams.UpdateTeam;
using Turniejo.Teams.Application.Teams.UpdateTeamLogo;
using Turniejo.Teams.Application.Teams.UploadTeamLogo;

namespace Turniejo.Api.Controllers
{
    [Route("api/organizations/{organizationId}/teams")]
    [ApiController]
    public sealed class TeamsController(ISender sender) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromRoute] Guid organizationId, [FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateTeamCommand(organizationId, request.Name, request.ShortName, request.LogoUrl);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/organizations/{organizationId}/teams/{result.Value.TeamId}", result.Value);
        }

        [HttpPut("{teamId}")]
        public async Task<IActionResult> UpdateTeam([FromRoute] Guid organizationId, [FromRoute] Guid teamId, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTeamCommand(organizationId, teamId, request.Name, request.ShortName);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{teamId}/logo")]
        public async Task<IActionResult> UpdateTeamLogo([FromRoute] Guid organizationId, [FromRoute] Guid teamId, [FromBody] UpdateTeamLogoRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTeamLogoCommand(organizationId, teamId, request.LogoUrl);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Plik z herbem (multipart, pole "file"). Zwraca adres do zapisania jako LogoUrl drużyny.
        [HttpPost("logos")]
        [EnableRateLimiting(RateLimitingExtensions.Uploads)]
        [RequestSizeLimit(UploadTeamLogoCommand.MaxBytes + 64 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = UploadTeamLogoCommand.MaxBytes + 64 * 1024)]
        public async Task<IActionResult> UploadTeamLogo([FromRoute] Guid organizationId, IFormFile? file, CancellationToken cancellationToken)
        {
            byte[] content = [];
            if (file is { Length: > 0 and <= UploadTeamLogoCommand.MaxBytes })
            {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }
            else if (file is { Length: > UploadTeamLogoCommand.MaxBytes })
            {
                // Validator zgłosi "za duży" bez kopiowania całego pliku do pamięci.
                content = new byte[UploadTeamLogoCommand.MaxBytes + 1];
            }

            var result = await sender.Send(new UploadTeamLogoCommand(organizationId, content), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams([FromRoute] Guid organizationId, CancellationToken cancellationToken)
        {
            var query = new GetTeamsQuery(organizationId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{teamId}")]
        public async Task<IActionResult> GetTeamById([FromRoute] Guid organizationId, [FromRoute] Guid teamId, CancellationToken cancellationToken)
        {
            var query = new GetTeamByIdQuery(organizationId, teamId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record CreateTeamRequest(string Name, string? ShortName, string? LogoUrl);
        public sealed record UpdateTeamRequest(string Name, string? ShortName);
        public sealed record UpdateTeamLogoRequest(string? LogoUrl);
    }
}
