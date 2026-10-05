using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.CorrectFinishedMatchScore
{
    internal sealed class CorrectFinishedMatchScoreCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<CorrectFinishedMatchScoreCommand, CorrectFinishedMatchScoreResponse>
    {
        public async Task<Result<CorrectFinishedMatchScoreResponse>> Handle(CorrectFinishedMatchScoreCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<CorrectFinishedMatchScoreResponse>(Error.NotFound("Match", request.MatchId));

            var oldValue = JsonSerializer.Serialize(new { match.HomeScore, match.AwayScore, match.WinnerTournamentTeamId });

            var result = match.CorrectFinishedScore(request.HomeScore, request.AwayScore, request.WinnerTournamentTeamId, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<CorrectFinishedMatchScoreResponse>(result.Error);

            var newValue = JsonSerializer.Serialize(new { match.HomeScore, match.AwayScore, match.WinnerTournamentTeamId });

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "Match", match.Id, "CorrectFinishedScore", oldValue, newValue);
            if (auditLogResult.IsFailure)
                return Result.Failure<CorrectFinishedMatchScoreResponse>(auditLogResult.Error);

            await dbContext.AddAuditLogAsync(auditLogResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CorrectFinishedMatchScoreResponse(match.Id, match.HomeScore, match.AwayScore));
        }
    }
}
