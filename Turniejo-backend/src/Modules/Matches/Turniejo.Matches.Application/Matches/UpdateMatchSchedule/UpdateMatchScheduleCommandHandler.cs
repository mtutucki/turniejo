using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.UpdateMatchSchedule
{
    internal sealed class UpdateMatchScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ScheduleConflictChecker conflictChecker,
        PlannedSlotShifter plannedSlotShifter,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdateMatchScheduleCommand, UpdateMatchScheduleResponse>
    {
        public async Task<Result<UpdateMatchScheduleResponse>> Handle(UpdateMatchScheduleCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<UpdateMatchScheduleResponse>(Error.NotFound("Match", request.MatchId));

            var conflict = await conflictChecker.FindConflictAsync(match, request.PitchId, request.ScheduledAt, "PitchId", cancellationToken);
            if (conflict != null)
                return Result.Failure<UpdateMatchScheduleResponse>(conflict);

            var result = match.UpdateSchedule(request.PitchId, request.ScheduledAt, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<UpdateMatchScheduleResponse>(result.Error);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await plannedSlotShifter.ShiftAsync(match.TournamentId, cancellationToken);

            return Result.Success(new UpdateMatchScheduleResponse(match.Id));
        }
    }
}
