using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.UpdateMatchEvent
{
    internal sealed class UpdateMatchEventCommandHandler(
        IMatchesDbContext dbContext,
        ISender sender,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdateMatchEventCommand, UpdateMatchEventResponse>
    {
        public async Task<Result<UpdateMatchEventResponse>> Handle(UpdateMatchEventCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<UpdateMatchEventResponse>(Error.NotFound("Match", request.MatchId));

            var matchEvent = await dbContext.GetMatchEventByIdAsync(request.MatchEventId, cancellationToken);
            if (matchEvent == null || matchEvent.MatchId != request.MatchId)
                return Result.Failure<UpdateMatchEventResponse>(Error.NotFound("MatchEvent", request.MatchEventId));

            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure)
                return Result.Failure<UpdateMatchEventResponse>(editableCheck.Error);

            var oldValue = JsonSerializer.Serialize(new
            {
                matchEvent.Type,
                matchEvent.Minute,
                matchEvent.Second,
                matchEvent.PlayerId,
                matchEvent.RelatedPlayerId
            });

            var result = matchEvent.UpdateDetails(
                request.Type, request.Minute, request.Second, request.PlayerId, request.RelatedPlayerId,
                currentUserContext.UserId, request.TournamentId);
            if (result.IsFailure)
                return Result.Failure<UpdateMatchEventResponse>(result.Error);

            var newValue = JsonSerializer.Serialize(new
            {
                matchEvent.Type,
                matchEvent.Minute,
                matchEvent.Second,
                matchEvent.PlayerId,
                matchEvent.RelatedPlayerId
            });

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "MatchEvent", matchEvent.Id, "Update", oldValue, newValue);
            if (auditLogResult.IsFailure)
                return Result.Failure<UpdateMatchEventResponse>(auditLogResult.Error);

            await dbContext.AddAuditLogAsync(auditLogResult.Value, cancellationToken);

            foreach (var domainEvent in matchEvent.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var recalcResult = await sender.Send(new RecalculateMatchScoreCommand(match.Id), cancellationToken);
            if (recalcResult.IsFailure)
                return Result.Failure<UpdateMatchEventResponse>(recalcResult.Error);

            return Result.Success(new UpdateMatchEventResponse(matchEvent.Id, recalcResult.Value.HomeScore, recalcResult.Value.AwayScore));
        }
    }
}
