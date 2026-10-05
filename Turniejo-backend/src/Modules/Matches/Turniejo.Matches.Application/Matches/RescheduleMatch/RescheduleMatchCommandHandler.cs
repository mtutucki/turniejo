using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.RescheduleMatch
{
    internal sealed class RescheduleMatchCommandHandler(
        IMatchesDbContext dbContext,
        ScheduleConflictChecker conflictChecker,
        PlannedSlotShifter plannedSlotShifter,
        ICurrentUserContext currentUserContext) : ICommandHandler<RescheduleMatchCommand, RescheduleMatchResponse>
    {
        public async Task<Result<RescheduleMatchResponse>> Handle(RescheduleMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<RescheduleMatchResponse>(Error.NotFound("Match", request.MatchId));

            var conflict = await conflictChecker.FindConflictAsync(match, match.PitchId, request.ScheduledAt, "ScheduledAt", cancellationToken);
            if (conflict != null)
                return Result.Failure<RescheduleMatchResponse>(conflict);

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
            await plannedSlotShifter.ShiftAsync(match.TournamentId, cancellationToken);

            return Result.Success(new RescheduleMatchResponse(match.Id));
        }
    }
}
