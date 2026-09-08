using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.PauseMatch
{
    internal sealed class PauseMatchCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<PauseMatchCommand, PauseMatchResponse>
    {
        public async Task<Result<PauseMatchResponse>> Handle(PauseMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<PauseMatchResponse>(Error.NotFound("Match", request.MatchId));

            var result = match.Pause(currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<PauseMatchResponse>(result.Error);

            var elapsedSeconds = match.GetElapsedSeconds(match.PausedAt!.Value);
            var matchEventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.MatchPaused, elapsedSeconds / 60, currentUserContext.UserId,
                second: elapsedSeconds % 60);
            if (matchEventResult.IsFailure)
                return Result.Failure<PauseMatchResponse>(matchEventResult.Error);

            await dbContext.AddMatchEventAsync(matchEventResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new PauseMatchResponse(match.Id));
        }
    }
}
