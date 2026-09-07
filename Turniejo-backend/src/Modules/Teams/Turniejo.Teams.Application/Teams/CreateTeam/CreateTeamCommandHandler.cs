using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Application.Teams.CreateTeam
{
    internal sealed class CreateTeamCommandHandler(ITeamsDbContext dbContext, ICurrentUserContext currentUserContext) : ICommandHandler<CreateTeamCommand, CreateTeamResponse>
    {
        public async Task<Result<CreateTeamResponse>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
        {
            var result = Team.Create(
                Guid.NewGuid(),
                request.OrganizationId,
                currentUserContext.UserId,
                request.Name,
                request.ShortName,
                request.LogoUrl);

            if (result.IsFailure)
                return Result.Failure<CreateTeamResponse>(result.Error);

            var team = result.Value;

            await dbContext.AddTeamAsync(team, cancellationToken);

            foreach (var domainEvent in team.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateTeamResponse(team.Id));
        }
    }
}
