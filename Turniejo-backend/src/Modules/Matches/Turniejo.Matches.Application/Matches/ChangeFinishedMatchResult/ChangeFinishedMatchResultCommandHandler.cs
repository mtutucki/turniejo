using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Entities;
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

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<ChangeFinishedMatchResultResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            // Okno poprawek dotyczy sędziego. Organizator rozstrzyga też później, np. przyznaje walkower
            // za nieuprawnionego zawodnika.
            var isOrganizer = await authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId, nameof(RoleScopeType.Tournament), match.TournamentId, MatchRoles.OrganizerOnly, cancellationToken);

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var (goalsHome, goalsAway) = MatchGoalScore.FromEvents(match, events);

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
    }
}
