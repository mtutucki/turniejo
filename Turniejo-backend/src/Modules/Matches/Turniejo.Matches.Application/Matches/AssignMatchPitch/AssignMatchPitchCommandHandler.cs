using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AssignMatchPitch
{
    internal sealed class AssignMatchPitchCommandHandler(
        IMatchesDbContext dbContext,
        ScheduleConflictChecker conflictChecker,
        PlannedSlotShifter plannedSlotShifter,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignMatchPitchCommand, AssignMatchPitchResponse>
    {
        public async Task<Result<AssignMatchPitchResponse>> Handle(AssignMatchPitchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AssignMatchPitchResponse>(Error.NotFound("Match", request.MatchId));

            var conflict = await conflictChecker.FindConflictAsync(match, request.PitchId, match.ScheduledAt, "PitchId", cancellationToken);
            if (conflict != null)
                return Result.Failure<AssignMatchPitchResponse>(conflict);

            var result = match.AssignPitch(request.PitchId, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<AssignMatchPitchResponse>(result.Error);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await plannedSlotShifter.ShiftAsync(match.TournamentId, cancellationToken);

            return Result.Success(new AssignMatchPitchResponse(match.Id));
        }
    }
}
