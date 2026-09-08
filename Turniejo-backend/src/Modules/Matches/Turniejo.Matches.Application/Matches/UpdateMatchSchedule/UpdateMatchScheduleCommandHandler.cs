using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.UpdateMatchSchedule
{
    internal sealed class UpdateMatchScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdateMatchScheduleCommand, UpdateMatchScheduleResponse>
    {
        public async Task<Result<UpdateMatchScheduleResponse>> Handle(UpdateMatchScheduleCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<UpdateMatchScheduleResponse>(Error.NotFound("Match", request.MatchId));

            if (request.PitchId.HasValue)
            {
                var conflict = await dbContext.MatchExistsAtPitchAndTimeAsync(request.PitchId.Value, request.ScheduledAt, match.Id, cancellationToken);
                if (conflict)
                    return Result.Failure<UpdateMatchScheduleResponse>(Error.Validation("PitchId", "Another match is already scheduled on this pitch at this time"));
            }

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

            return Result.Success(new UpdateMatchScheduleResponse(match.Id));
        }
    }
}
