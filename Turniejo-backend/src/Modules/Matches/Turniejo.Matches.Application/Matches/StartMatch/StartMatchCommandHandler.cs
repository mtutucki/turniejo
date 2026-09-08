using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.StartMatch
{
    internal sealed class StartMatchCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<StartMatchCommand, StartMatchResponse>
    {
        public async Task<Result<StartMatchResponse>> Handle(StartMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<StartMatchResponse>(Error.NotFound("Match", request.MatchId));

            var result = match.Start(currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<StartMatchResponse>(result.Error);

            var matchEventResult = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.MatchStarted, 0, currentUserContext.UserId);
            if (matchEventResult.IsFailure)
                return Result.Failure<StartMatchResponse>(matchEventResult.Error);

            await dbContext.AddMatchEventAsync(matchEventResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new StartMatchResponse(match.Id));
        }
    }
}
