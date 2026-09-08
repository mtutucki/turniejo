using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AssignMatchPitch
{
    internal sealed class AssignMatchPitchCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignMatchPitchCommand, AssignMatchPitchResponse>
    {
        public async Task<Result<AssignMatchPitchResponse>> Handle(AssignMatchPitchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AssignMatchPitchResponse>(Error.NotFound("Match", request.MatchId));

            if (request.PitchId.HasValue)
            {
                var conflict = await dbContext.MatchExistsAtPitchAndTimeAsync(request.PitchId.Value, match.ScheduledAt, match.Id, cancellationToken);
                if (conflict)
                    return Result.Failure<AssignMatchPitchResponse>(Error.Validation("PitchId", "Another match is already scheduled on this pitch at this time"));
            }

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

            return Result.Success(new AssignMatchPitchResponse(match.Id));
        }
    }
}
