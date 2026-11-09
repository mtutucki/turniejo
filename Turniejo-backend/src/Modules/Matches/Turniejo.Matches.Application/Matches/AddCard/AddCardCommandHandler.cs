using Turniejo.Matches.Domain.Enums;
using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AddCard
{
    internal sealed class AddCardCommandHandler(
        IMatchesDbContext dbContext,
        MatchEventRosterGuard rosterGuard,
        SendOffRules sendOffRules,
        ICurrentUserContext currentUserContext) : ICommandHandler<AddCardCommand, AddCardResponse>
    {
        public async Task<Result<AddCardResponse>> Handle(AddCardCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AddCardResponse>(Error.NotFound("Match", request.MatchId));

            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure)
                return Result.Failure<AddCardResponse>(editableCheck.Error);

            if (request.TournamentTeamId != match.HomeTournamentTeamId && request.TournamentTeamId != match.AwayTournamentTeamId)
                return Result.Failure<AddCardResponse>(Error.Validation("TournamentTeamId", "Team is not playing in this match"));

            var playerCheck = await rosterGuard.EnsureOnRosterAsync(request.TournamentTeamId, request.PlayerId, "PlayerId", cancellationToken);
            if (playerCheck.IsFailure)
                return Result.Failure<AddCardResponse>(playerCheck.Error);

            var sentOff = await sendOffRules.EnsureNotSentOffAsync(match.Id, request.PlayerId, "PlayerId", cancellationToken);
            if (sentOff != null)
                return Result.Failure<AddCardResponse>(sentOff);

            var cardType = await sendOffRules.ResolveCardTypeAsync(match.Id, request.PlayerId, request.CardType, cancellationToken);

            var eventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, cardType, request.Minute, currentUserContext.UserId,
                request.TournamentTeamId, request.PlayerId, second: request.Second,
                isSecondYellow: request.CardType == MatchEventType.YellowCard && cardType == MatchEventType.RedCard);
            if (eventResult.IsFailure)
                return Result.Failure<AddCardResponse>(eventResult.Error);

            await dbContext.AddMatchEventAsync(eventResult.Value, cancellationToken);

            var addedEvent = new MatchEventAdded(eventResult.Value.Id, match.Id, match.TournamentId, cardType.ToString());
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(addedEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(addedEvent, addedEvent.GetType())),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddCardResponse(eventResult.Value.Id, cardType.ToString()));
        }
    }
}
