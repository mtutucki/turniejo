using System.Text.Json;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat
{
    internal sealed class SetTournamentFormatCommandHandler(ITournamentsDbContext tournamentsDbContext) : ICommandHandler<SetTournamentFormatCommand, SetTournamentFormatResponse>
    {
        public async Task<Result<SetTournamentFormatResponse>> Handle(SetTournamentFormatCommand request, CancellationToken cancellationToken)
        {
            var result = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (result == null)
                return Result.Failure<SetTournamentFormatResponse>(Error.NotFound("Tournament", request.TournamentId));

            var setFormatResult = result.SetFormat(request.TournamentFormat);

            if (setFormatResult.IsFailure)
                return Result.Failure<SetTournamentFormatResponse>(setFormatResult.Error);

            foreach (var domainEvent in result.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            result.ClearDomainEvents();

            return Result.Success(new SetTournamentFormatResponse(result.Id, result.Slug));
        }
    }
}
