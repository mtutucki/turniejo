using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.SetMatchScore
{
    internal sealed class SetMatchScoreCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<SetMatchScoreCommand, SetMatchScoreResponse>
    {
        public async Task<Result<SetMatchScoreResponse>> Handle(SetMatchScoreCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<SetMatchScoreResponse>(Error.NotFound("Match", request.MatchId));

            var oldValue = JsonSerializer.Serialize(new { match.HomeScore, match.AwayScore, match.ResultType });

            var result = match.SetManualScore(request.HomeScore, request.AwayScore, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<SetMatchScoreResponse>(result.Error);

            var newValue = JsonSerializer.Serialize(new { match.HomeScore, match.AwayScore, match.ResultType });

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "Match", match.Id, "SetManualScore", oldValue, newValue);
            if (auditLogResult.IsFailure)
                return Result.Failure<SetMatchScoreResponse>(auditLogResult.Error);

            await dbContext.AddAuditLogAsync(auditLogResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new SetMatchScoreResponse(match.Id, match.HomeScore, match.AwayScore));
        }
    }
}
