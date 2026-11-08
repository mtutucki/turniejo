using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AddGoal
{
    internal sealed class AddGoalCommandHandler(
        IMatchesDbContext dbContext,
        MatchEventRosterGuard rosterGuard,
        SendOffRules sendOffRules,
        ISender sender,
        ICurrentUserContext currentUserContext) : ICommandHandler<AddGoalCommand, AddGoalResponse>
    {
        public async Task<Result<AddGoalResponse>> Handle(AddGoalCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AddGoalResponse>(Error.NotFound("Match", request.MatchId));

            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure)
                return Result.Failure<AddGoalResponse>(editableCheck.Error);

            if (request.TournamentTeamId != match.HomeTournamentTeamId && request.TournamentTeamId != match.AwayTournamentTeamId)
                return Result.Failure<AddGoalResponse>(Error.Validation("TournamentTeamId", "Team is not playing in this match"));

            var scorerCheck = await rosterGuard.EnsureOnRosterAsync(request.TournamentTeamId, request.PlayerId, "PlayerId", cancellationToken);
            if (scorerCheck.IsFailure)
                return Result.Failure<AddGoalResponse>(scorerCheck.Error);

            var assistCheck = await rosterGuard.EnsureOnRosterAsync(request.TournamentTeamId, request.AssistPlayerId, "AssistPlayerId", cancellationToken);
            if (assistCheck.IsFailure)
                return Result.Failure<AddGoalResponse>(assistCheck.Error);

            var scorerSentOff = await sendOffRules.EnsureNotSentOffAsync(match.Id, request.PlayerId, "PlayerId", cancellationToken);
            if (scorerSentOff != null)
                return Result.Failure<AddGoalResponse>(scorerSentOff);

            var assistSentOff = await sendOffRules.EnsureNotSentOffAsync(match.Id, request.AssistPlayerId, "AssistPlayerId", cancellationToken);
            if (assistSentOff != null)
                return Result.Failure<AddGoalResponse>(assistSentOff);

            var eventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, request.GoalType, request.Minute, currentUserContext.UserId,
                request.TournamentTeamId, request.PlayerId, request.AssistPlayerId, request.Second);
            if (eventResult.IsFailure)
                return Result.Failure<AddGoalResponse>(eventResult.Error);

            var drawCheck = await MatchGoalScore.EnsureKnockoutStaysDecidedAsync(
                dbContext, match, events => events.Append(eventResult.Value), cancellationToken);
            if (drawCheck.IsFailure)
                return Result.Failure<AddGoalResponse>(drawCheck.Error);

            await dbContext.AddMatchEventAsync(eventResult.Value, cancellationToken);

            var addedEvent = new MatchEventAdded(eventResult.Value.Id, match.Id, match.TournamentId, request.GoalType.ToString());
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(addedEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(addedEvent, addedEvent.GetType())),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            var recalcResult = await sender.Send(new RecalculateMatchScoreCommand(match.Id), cancellationToken);
            if (recalcResult.IsFailure)
                return Result.Failure<AddGoalResponse>(recalcResult.Error);

            return Result.Success(new AddGoalResponse(eventResult.Value.Id, recalcResult.Value.HomeScore, recalcResult.Value.AwayScore));
        }
    }
}
