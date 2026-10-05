using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Organizations.Application.Organizations.CreateOrganization;

internal sealed class CreateOrganizationCommandHandler(
    IOrganizationsDbContext dbContext,
    OrganizationProvisioner provisioner,
    IUserModule userModule)
    : ICommandHandler<CreateOrganizationCommand, CreateOrganizationResponse>
{
    public async Task<Result<CreateOrganizationResponse>> Handle(
        CreateOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var result = await provisioner.CreateAsync(request.OwnerUserId, request.Name, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<CreateOrganizationResponse>(result.Error);

        var organization = result.Value;

        await userModule.AssignRoleAsync(
            organization.OwnerUserId,
            nameof(UserRole.Organizer),
            nameof(RoleScopeType.Organization),
            organization.Id,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateOrganizationResponse(
            organization.Id, organization.Slug));
    }
}