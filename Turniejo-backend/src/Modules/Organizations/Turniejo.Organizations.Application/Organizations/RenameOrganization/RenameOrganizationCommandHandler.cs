using Turniejo.Organizations.Application.Abstractions;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Organizations.Application.Organizations.RenameOrganization;

internal sealed class RenameOrganizationCommandHandler(IOrganizationsDbContext dbContext)
    : ICommandHandler<RenameOrganizationCommand>
{
    public async Task<Result> Handle(RenameOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await dbContext.GetOrganizationByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null)
            return Result.Failure(Error.NotFound("Organization", request.OrganizationId));

        var result = organization.Rename(request.Name);
        if (result.IsFailure)
            return result;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
