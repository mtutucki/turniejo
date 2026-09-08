using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.RescheduleMatch
{
    internal sealed class RescheduleMatchCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<RescheduleMatchCommand, RescheduleMatchResponse>
    {
        public async Task<Result<RescheduleMatchResponse>> Handle(RescheduleMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<RescheduleMatchResponse>(Error.NotFound("Match", request.MatchId));

            if (match.PitchId.HasValue)
            {
                var conflict = await dbContext.MatchExistsAtPitchAndTimeAsync(match.PitchId.Value, request.ScheduledAt, match.Id, cancellationToken);
                if (conflict)
                    return Result.Failure<RescheduleMatchResponse>(Error.Validation("ScheduledAt", "Another match is already scheduled on this pitch at this time"));
            }

            var result = match.Reschedule(request.ScheduledAt, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<RescheduleMatchResponse>(result.Error);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new RescheduleMatchResponse(match.Id));
        }
    }
}
