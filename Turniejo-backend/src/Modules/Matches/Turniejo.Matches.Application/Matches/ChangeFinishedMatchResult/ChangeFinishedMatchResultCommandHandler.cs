using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.ChangeFinishedMatchResult
{
    internal sealed class ChangeFinishedMatchResultCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext,
        IAuthorizationService authorizationService) : ICommandHandler<ChangeFinishedMatchResultCommand, ChangeFinishedMatchResultResponse>
    {
        public async Task<Result<ChangeFinishedMatchResultResponse>> Handle(ChangeFinishedMatchResultCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<ChangeFinishedMatchResultResponse>(Error.NotFound("Match", request.MatchId));

            // Po zakończeniu (albo anulowaniu) turnieju wyniki są ostateczne - także dla organizatora.
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament?.Status is "Finished" or "Cancelled")
                return Result.Failure<ChangeFinishedMatchResultResponse>(
                    Error.Conflict("Status", "The tournament has finished or been cancelled"));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<ChangeFinishedMatchResultResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            // Okno poprawek dotyczy sędziego. Organizator rozstrzyga też później, np. przyznaje walkower
            // za nieuprawnionego zawodnika.
            var isOrganizer = await authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId, nameof(RoleScopeType.Tournament), match.TournamentId, MatchRoles.OrganizerOnly, cancellationToken);

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var (currentHome, currentAway) = MatchGoalScore.FromEvents(match, events);

            // Walkower ma wynik z regulaminu, więc goli z boiska wtedy nie ruszamy.
            var changeGoals = request.ResultType != MatchResultType.Walkover;
            var goalsHome = changeGoals ? request.HomeScore ?? currentHome : currentHome;
            var goalsAway = changeGoals ? request.AwayScore ?? currentAway : currentAway;

            var oldValue = JsonSerializer.Serialize(new
            {
                ResultType = match.ResultType.ToString(), match.HomeScore, match.AwayScore,
                match.HomePenaltyScore, match.AwayPenaltyScore, match.WinnerTournamentTeamId,
            });

            var result = match.ChangeFinishedResult(
                request.ResultType, request.WinnerTournamentTeamId, goalsHome, goalsAway,
                ignoreEditWindow: isOrganizer, currentUserContext.UserId,
                settings.WalkoverWinnerScore, settings.WalkoverLoserScore,
                request.HomePenaltyScore, request.AwayPenaltyScore);
            if (result.IsFailure)
                return Result.Failure<ChangeFinishedMatchResultResponse>(result.Error);

            if (goalsHome != currentHome || goalsAway != currentAway)
            {
                await AdjustGoalsAsync(match, events, match.HomeTournamentTeamId, goalsHome - currentHome, cancellationToken);
                await AdjustGoalsAsync(match, events, match.AwayTournamentTeamId, goalsAway - currentAway, cancellationToken);
            }

            var newValue = JsonSerializer.Serialize(new
            {
                ResultType = match.ResultType.ToString(), match.HomeScore, match.AwayScore,
                match.HomePenaltyScore, match.AwayPenaltyScore, match.WinnerTournamentTeamId,
            });

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "Match", match.Id, "ChangeFinishedResult", oldValue, newValue);
            if (auditLogResult.IsFailure)
                return Result.Failure<ChangeFinishedMatchResultResponse>(auditLogResult.Error);

            await dbContext.AddAuditLogAsync(auditLogResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new ChangeFinishedMatchResultResponse(match.Id, match.HomeScore, match.AwayScore, match.WinnerTournamentTeamId));
        }

        /// <summary>
        /// Dopisuje (difference &gt; 0) gole bez strzelca albo cofa (difference &lt; 0) gole zaliczone drużynie.
        /// Cofamy najpierw gole bez strzelca, potem najpóźniejsze, żeby nie gubić uzupełnionych statystyk.
        /// </summary>
        private async Task AdjustGoalsAsync(
            Match match, IReadOnlyList<MatchEvent> events, Guid teamId, int difference, CancellationToken cancellationToken)
        {
            var userId = currentUserContext.UserId;

            for (var i = 0; i < difference; i++)
            {
                var goal = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 0, userId, teamId).Value;
                await dbContext.AddMatchEventAsync(goal, cancellationToken);
            }

            if (difference >= 0)
                return;

            var toRevert = events
                .Where(e => ScoresFor(match, e) == teamId)
                .OrderBy(e => e.PlayerId.HasValue)
                .ThenByDescending(e => e.Minute)
                .ThenByDescending(e => e.Second ?? 0)
                .Take(-difference)
                .Select(e => e.Id)
                .ToList();

            foreach (var eventId in toRevert)
            {
                // Lista zdarzeń jest bez śledzenia zmian, więc usuwane zdarzenie pobieramy jeszcze raz.
                var tracked = await dbContext.GetMatchEventByIdAsync(eventId, cancellationToken);
                if (tracked == null || tracked.MarkDeleted(userId, match.TournamentId).IsFailure)
                    continue;

                foreach (var domainEvent in tracked.DomainEvents)
                {
                    await dbContext.AddOutboxMessageAsync(
                        OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                        cancellationToken);
                }
            }
        }

        private static Guid? ScoresFor(Match match, MatchEvent matchEvent) => matchEvent.Type switch
        {
            MatchEventType.Goal or MatchEventType.PenaltyGoal => matchEvent.TournamentTeamId,
            MatchEventType.OwnGoal when matchEvent.TournamentTeamId == match.HomeTournamentTeamId => match.AwayTournamentTeamId,
            MatchEventType.OwnGoal when matchEvent.TournamentTeamId == match.AwayTournamentTeamId => match.HomeTournamentTeamId,
            _ => null,
        };
    }
}
