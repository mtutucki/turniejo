using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AssignRefereeToMatch
{
    internal sealed class AssignRefereeToMatchCommandHandler(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        IAuthorizationService authorizationService,
        ScheduleConflictChecker conflictChecker,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignRefereeToMatchCommand, AssignRefereeToMatchResponse>
    {
        public async Task<Result<AssignRefereeToMatchResponse>> Handle(AssignRefereeToMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AssignRefereeToMatchResponse>(Error.NotFound("Match", request.MatchId));

            // Bez tego organizator mógł wskazać dowolne konto, a AssignRoleAsync nadawało mu rolę Referee
            // w meczu — czyli dostęp do panelu sędziego komuś, kto nie dołączył do turnieju kodem sędziego.
            if (request.RefereeId.HasValue)
            {
                var isTournamentReferee = await authorizationService.HasAnyRoleAsync(
                    request.RefereeId.Value,
                    nameof(RoleScopeType.Tournament),
                    request.TournamentId,
                    [nameof(UserRole.Referee)],
                    cancellationToken);
                if (!isTournamentReferee)
                    return Result.Failure<AssignRefereeToMatchResponse>(Error.Validation("RefereeId", "User is not a referee in this tournament"));

                if (request.RefereeId != match.RefereeId)
                {
                    var conflict = await conflictChecker.FindRefereeConflictAsync(match, request.RefereeId.Value, cancellationToken);
                    if (conflict != null)
                        return Result.Failure<AssignRefereeToMatchResponse>(conflict);
                }
            }

            var previousRefereeId = match.RefereeId;

            var result = match.AssignReferee(request.RefereeId, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<AssignRefereeToMatchResponse>(result.Error);

            // Bez tego zdjęty/zmieniony sędzia zachowywał UserRoleAssignment(Referee, Match, MatchId)
            // i mógł dalej prowadzić mecz (start/pauza/eventy) mimo usunięcia z Match.RefereeId.
            if (previousRefereeId.HasValue && previousRefereeId != request.RefereeId)
            {
                await userModule.RevokeRoleAsync(
                    previousRefereeId.Value,
                    nameof(UserRole.Referee),
                    nameof(RoleScopeType.Match),
                    match.Id,
                    cancellationToken);
            }

            if (request.RefereeId.HasValue)
            {
                await userModule.AssignRoleAsync(
                    request.RefereeId.Value,
                    nameof(UserRole.Referee),
                    nameof(RoleScopeType.Match),
                    match.Id,
                    cancellationToken);
            }

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignRefereeToMatchResponse(match.Id));
        }
    }
}
