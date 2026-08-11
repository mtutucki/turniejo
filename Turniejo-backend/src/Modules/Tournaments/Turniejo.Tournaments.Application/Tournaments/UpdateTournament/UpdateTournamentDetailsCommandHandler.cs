using System.Text.Json;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournament
{
    internal sealed class UpdateTournamentDetailsCommandHandler(ITournamentsDbContext dbContext) : ICommandHandler<UpdateTournamentDetailsCommand, UpdateTournamentDetailsResponse>
    {
        public async Task<Result<UpdateTournamentDetailsResponse>> Handle(UpdateTournamentDetailsCommand request, CancellationToken cancellationToken)
        {
            var result = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (result == null)
                return Result.Failure<UpdateTournamentDetailsResponse>(Error.NotFound("Tournament", request.TournamentId));

            var updateResult = result.UpdateDetails(request.Name, request.StartAt, request.EndAt, request.TeamLimit, request.Description);

            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentDetailsResponse>(updateResult.Error);

            foreach (var domainEvent in result.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            result.ClearDomainEvents();
            return Result.Success(new UpdateTournamentDetailsResponse(result.Id, result.Slug));
        }
    }
}
