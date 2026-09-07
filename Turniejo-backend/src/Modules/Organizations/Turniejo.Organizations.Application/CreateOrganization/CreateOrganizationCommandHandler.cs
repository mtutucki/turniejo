using System.Text.Json;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.Organizations.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Organizations.Application.Organizations.CreateOrganization;

internal sealed class CreateOrganizationCommandHandler(
    IOrganizationsDbContext dbContext,
    IUserModule userModule)
    : ICommandHandler<CreateOrganizationCommand, CreateOrganizationResponse>
{
    public async Task<Result<CreateOrganizationResponse>> Handle(
        CreateOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.OrganizationExistsForOwnerAsync(
            request.OwnerUserId, cancellationToken);

        if (exists)
            return Result.Failure<CreateOrganizationResponse>(
                Error.Validation("Organization",
                    "You already have an organization."));

        var result = Organization.Create(request.OwnerUserId, request.Name);

        if (result.IsFailure)
            return Result.Failure<CreateOrganizationResponse>(result.Error);

        var organization = result.Value;

        await dbContext.AddOrganizationAsync(organization, cancellationToken);

        var outboxMessage = OutboxMessage.Create(
            typeof(OrganizationCreated).AssemblyQualifiedName!,
            JsonSerializer.Serialize(new OrganizationCreated(
                organization.Id,
                organization.OwnerUserId,
                organization.Name)));

        await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);

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