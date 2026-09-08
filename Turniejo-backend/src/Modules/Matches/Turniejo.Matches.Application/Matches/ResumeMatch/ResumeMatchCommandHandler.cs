using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.ResumeMatch
{
    internal sealed class ResumeMatchCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<ResumeMatchCommand, ResumeMatchResponse>
    {
        public async Task<Result<ResumeMatchResponse>> Handle(ResumeMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<ResumeMatchResponse>(Error.NotFound("Match", request.MatchId));

            var result = match.Resume(currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<ResumeMatchResponse>(result.Error);

            var elapsedSeconds = match.GetElapsedSeconds(DateTimeOffset.UtcNow);
            var matchEventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.MatchResumed, elapsedSeconds / 60, currentUserContext.UserId,
                second: elapsedSeconds % 60);
            if (matchEventResult.IsFailure)
                return Result.Failure<ResumeMatchResponse>(matchEventResult.Error);

            await dbContext.AddMatchEventAsync(matchEventResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new ResumeMatchResponse(match.Id));
        }
    }
}
