using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.SetGoalPlayers
{
    internal sealed class SetGoalPlayersCommandHandler(
        IMatchesDbContext dbContext,
        MatchEventRosterGuard rosterGuard,
        ICurrentUserContext currentUserContext,
        IAuthorizationService authorizationService) : ICommandHandler<SetGoalPlayersCommand, SetGoalPlayersResponse>
    {
        private static readonly MatchEventType[] GoalTypes =
            [MatchEventType.Goal, MatchEventType.PenaltyGoal, MatchEventType.OwnGoal];

        public async Task<Result<SetGoalPlayersResponse>> Handle(SetGoalPlayersCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<SetGoalPlayersResponse>(Error.NotFound("Match", request.MatchId));

            var goal = await dbContext.GetMatchEventByIdAsync(request.MatchEventId, cancellationToken);
            if (goal == null || goal.MatchId != match.Id || goal.IsDeleted || !GoalTypes.Contains(goal.Type))
                return Result.Failure<SetGoalPlayersResponse>(Error.NotFound("MatchEvent", request.MatchEventId));

            // Strzelec to statystyka, nie wynik, więc organizator może go dopisać po zamknięciu okna sędziego
            // (tak samo jak MVP).
            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure && !(match.Status == MatchStatus.Finished && await IsOrganizerAsync(match.TournamentId, cancellationToken)))
                return Result.Failure<SetGoalPlayersResponse>(editableCheck.Error);

            if (goal.Type == MatchEventType.OwnGoal && request.AssistPlayerId.HasValue)
                return Result.Failure<SetGoalPlayersResponse>(Error.Validation("AssistPlayerId", "An own goal has no assist"));

            if (goal.TournamentTeamId is not { } teamId)
                return Result.Failure<SetGoalPlayersResponse>(Error.Validation("MatchEventId", "Goal has no team"));

            var scorerCheck = await rosterGuard.EnsureOnRosterAsync(teamId, request.PlayerId, "PlayerId", cancellationToken);
            if (scorerCheck.IsFailure)
                return Result.Failure<SetGoalPlayersResponse>(scorerCheck.Error);

            var assistCheck = await rosterGuard.EnsureOnRosterAsync(teamId, request.AssistPlayerId, "AssistPlayerId", cancellationToken);
            if (assistCheck.IsFailure)
                return Result.Failure<SetGoalPlayersResponse>(assistCheck.Error);

            var oldValue = JsonSerializer.Serialize(new { goal.PlayerId, goal.RelatedPlayerId });

            var updateResult = goal.UpdateDetails(
                goal.Type, goal.Minute, goal.Second, request.PlayerId, request.AssistPlayerId,
                currentUserContext.UserId, match.TournamentId);
            if (updateResult.IsFailure)
                return Result.Failure<SetGoalPlayersResponse>(updateResult.Error);

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "MatchEvent", goal.Id, "SetGoalPlayers", oldValue,
                JsonSerializer.Serialize(new { goal.PlayerId, goal.RelatedPlayerId }));
            if (auditLogResult.IsFailure)
                return Result.Failure<SetGoalPlayersResponse>(auditLogResult.Error);

            await dbContext.AddAuditLogAsync(auditLogResult.Value, cancellationToken);

            foreach (var domainEvent in goal.DomainEvents)
            {
                await dbContext.AddOutboxMessageAsync(
                    OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new SetGoalPlayersResponse(goal.Id, goal.PlayerId, goal.RelatedPlayerId));
        }

        private Task<bool> IsOrganizerAsync(Guid tournamentId, CancellationToken cancellationToken) =>
            authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId, nameof(RoleScopeType.Tournament), tournamentId, MatchRoles.OrganizerOnly, cancellationToken);
    }
}
