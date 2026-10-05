using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AddAssist
{
    // Asysta ma jedną ścieżkę zapisu: RelatedPlayerId na evencie gola/rzutu karnego. AddGoalCommand
    // ustawia je od razu przy strzale; ta komenda dopisuje asystę po fakcie do gola, który jeszcze
    // jej nie ma — nigdy nie tworzy osobnego, równoległego eventu typu Assist (wcześniej dwie
    // niezależne ścieżki zapisu groziły podwójnym naliczeniem tej samej asysty w rankingu).
    internal sealed class AddAssistCommandHandler(
        IMatchesDbContext dbContext,
        MatchEventRosterGuard rosterGuard,
        SendOffRules sendOffRules,
        ICurrentUserContext currentUserContext) : ICommandHandler<AddAssistCommand, AddAssistResponse>
    {
        public async Task<Result<AddAssistResponse>> Handle(AddAssistCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AddAssistResponse>(Error.NotFound("Match", request.MatchId));

            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure)
                return Result.Failure<AddAssistResponse>(editableCheck.Error);

            if (request.TournamentTeamId != match.HomeTournamentTeamId && request.TournamentTeamId != match.AwayTournamentTeamId)
                return Result.Failure<AddAssistResponse>(Error.Validation("TournamentTeamId", "Team is not playing in this match"));

            var assistCheck = await rosterGuard.EnsureOnRosterAsync(request.TournamentTeamId, request.PlayerId, "PlayerId", cancellationToken);
            if (assistCheck.IsFailure)
                return Result.Failure<AddAssistResponse>(assistCheck.Error);

            var scorerCheck = await rosterGuard.EnsureOnRosterAsync(request.TournamentTeamId, request.ScorerPlayerId, "ScorerPlayerId", cancellationToken);
            if (scorerCheck.IsFailure)
                return Result.Failure<AddAssistResponse>(scorerCheck.Error);

            var assistSentOff = await sendOffRules.EnsureNotSentOffAsync(match.Id, request.PlayerId, "PlayerId", cancellationToken);
            if (assistSentOff != null)
                return Result.Failure<AddAssistResponse>(assistSentOff);

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var goalEvent = events.FirstOrDefault(e =>
                !e.IsDeleted &&
                e.Type is MatchEventType.Goal or MatchEventType.PenaltyGoal &&
                e.TournamentTeamId == request.TournamentTeamId &&
                e.PlayerId == request.ScorerPlayerId &&
                e.Minute == request.Minute &&
                (!request.Second.HasValue || e.Second == request.Second) &&
                e.RelatedPlayerId == null);

            if (goalEvent == null)
                return Result.Failure<AddAssistResponse>(Error.Validation(
                    "ScorerPlayerId", "No goal without an assist was found for this player at this minute"));

            var updateResult = goalEvent.UpdateDetails(
                goalEvent.Type, goalEvent.Minute, goalEvent.Second, goalEvent.PlayerId, request.PlayerId,
                currentUserContext.UserId, request.TournamentId);
            if (updateResult.IsFailure)
                return Result.Failure<AddAssistResponse>(updateResult.Error);

            foreach (var domainEvent in goalEvent.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddAssistResponse(goalEvent.Id));
        }
    }
}
