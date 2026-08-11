using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus
{
    internal sealed class ChangeTournamentStatusCommandHandler(ITournamentsDbContext tournamentsDbContext, ICurrentUserContext currentUserContext) : ICommandHandler<ChangeTournamentStatusCommand, ChangeTournamentStatusResponse>
    {
        public async Task<Result<ChangeTournamentStatusResponse>> Handle(ChangeTournamentStatusCommand request, CancellationToken cancellationToken)
        {
            var result = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (result == null)
                return Result.Failure<ChangeTournamentStatusResponse>(Error.NotFound("Tournament", request.TournamentId));

            var changeStatusResult = result.ChangeStatus(request.TournamentStatus, currentUserContext.UserId);

            if (changeStatusResult.IsFailure)
                return Result.Failure<ChangeTournamentStatusResponse>(changeStatusResult.Error);

            foreach (var domainEvent in result.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            result.ClearDomainEvents();
            return Result.Success(new ChangeTournamentStatusResponse(result.Id, result.Slug));
        }
    }
}
