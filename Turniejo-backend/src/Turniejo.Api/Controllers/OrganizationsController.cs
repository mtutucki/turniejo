using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Organizations.Application.Organizations.CreateOrganization;
using Turniejo.Organizations.Application.Organizations.GetMyOrganization;
using Turniejo.Organizations.Application.Organizations.RenameOrganization;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Api.Controllers;

[ApiController]
[Route("api/organizations")]
public sealed class OrganizationsController(
    ISender sender,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateOrganizationCommand(currentUserContext.UserId, request.Name);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return result.ToActionResult();

        return Created($"/api/organizations/{result.Value.OrganizationId}", result.Value);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyOrganizationQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{organizationId:guid}")]
    public async Task<IActionResult> Rename([FromRoute] Guid organizationId, [FromBody] RenameOrganizationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RenameOrganizationCommand(organizationId, request.Name), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreateOrganizationRequest(string Name);
public sealed record RenameOrganizationRequest(string Name);