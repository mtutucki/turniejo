using Turniejo.Matches.Domain.Enums;
using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.RevertMatchEvent
{
    internal sealed class RevertMatchEventCommandHandler(
        IMatchesDbContext dbContext,
        ISender sender,
        ICurrentUserContext currentUserContext) : ICommandHandler<RevertMatchEventCommand, RevertMatchEventResponse>
    {
        public async Task<Result<RevertMatchEventResponse>> Handle(RevertMatchEventCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<RevertMatchEventResponse>(Error.NotFound("Match", request.MatchId));

            var matchEvent = await dbContext.GetMatchEventByIdAsync(request.MatchEventId, cancellationToken);
            if (matchEvent == null || matchEvent.MatchId != request.MatchId)
                return Result.Failure<RevertMatchEventResponse>(Error.NotFound("MatchEvent", request.MatchEventId));

            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure)
                return Result.Failure<RevertMatchEventResponse>(editableCheck.Error);

            var drawCheck = await MatchGoalScore.EnsureKnockoutStaysDecidedAsync(
                dbContext, match, events => events.Where(e => e.Id != matchEvent.Id), cancellationToken);
            if (drawCheck.IsFailure)
                return Result.Failure<RevertMatchEventResponse>(drawCheck.Error);

            var result = matchEvent.MarkDeleted(currentUserContext.UserId, request.TournamentId);
            if (result.IsFailure)
                return Result.Failure<RevertMatchEventResponse>(result.Error);

            // Cofnięta żółta: czerwona z drugiej żółtej tego zawodnika wraca do zwykłej żółtej (decyzja MNZ-015).
            if (matchEvent.Type == MatchEventType.YellowCard && matchEvent.PlayerId is { } playerId)
            {
                var redCards = await dbContext.GetActiveMatchEventsByTypeAsync(match.Id, MatchEventType.RedCard, cancellationToken);
                foreach (var red in redCards.Where(r => r.PlayerId == playerId && r.IsSecondYellow))
                {
                    red.RevertToYellow(currentUserContext.UserId, request.TournamentId);
                    foreach (var domainEvent in red.DomainEvents)
                        await dbContext.AddOutboxMessageAsync(
                            OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                            cancellationToken);
                    red.ClearDomainEvents();
                }
            }

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "MatchEvent", matchEvent.Id, "Revert",
                JsonSerializer.Serialize(new { IsDeleted = false }),
                JsonSerializer.Serialize(new { IsDeleted = true }));
            if (auditLogResult.IsFailure)
                return Result.Failure<RevertMatchEventResponse>(auditLogResult.Error);

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
                return Result.Failure<RevertMatchEventResponse>(recalcResult.Error);

            return Result.Success(new RevertMatchEventResponse(matchEvent.Id, recalcResult.Value.HomeScore, recalcResult.Value.AwayScore));
        }
    }
}
