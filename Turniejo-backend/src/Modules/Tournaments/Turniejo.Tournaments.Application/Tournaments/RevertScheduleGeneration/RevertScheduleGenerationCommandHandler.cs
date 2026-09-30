using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.RevertScheduleGeneration
{
    internal sealed class RevertScheduleGenerationCommandHandler(
        ITournamentsDbContext tournamentsDbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<RevertScheduleGenerationCommand>
    {
        public async Task<Result> Handle(RevertScheduleGenerationCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure(Error.NotFound("Tournament", request.TournamentId));

            var result = tournament.RevertScheduleGeneration(currentUserContext.UserId);
            if (result.IsFailure)
                return result;

            foreach (var domainEvent in tournament.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            tournament.ClearDomainEvents();
            return Result.Success();
        }
    }
}
