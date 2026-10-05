using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.SelectMatchMvp
{
    internal sealed class SelectMatchMvpCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext,
        IAuthorizationService authorizationService) : ICommandHandler<SelectMatchMvpCommand, SelectMatchMvpResponse>
    {
        public async Task<Result<SelectMatchMvpResponse>> Handle(SelectMatchMvpCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<SelectMatchMvpResponse>(Error.NotFound("Match", request.MatchId));

            // MVP to wyróżnienie, nie wynik — organizator może je uzupełnić po zamknięciu okna edycji sędziego.
            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure && !(match.Status == MatchStatus.Finished && await IsOrganizerAsync(match.TournamentId, cancellationToken)))
                return Result.Failure<SelectMatchMvpResponse>(editableCheck.Error);

            if (request.TournamentTeamId != match.HomeTournamentTeamId && request.TournamentTeamId != match.AwayTournamentTeamId)
                return Result.Failure<SelectMatchMvpResponse>(Error.Validation("TournamentTeamId", "Team is not playing in this match"));

            var existingMvpEvents = await dbContext.GetActiveMatchEventsByTypeAsync(match.Id, MatchEventType.MvpSelected, cancellationToken);
            foreach (var mvpEvent in existingMvpEvents)
            {
                mvpEvent.MarkDeleted(currentUserContext.UserId, request.TournamentId);

                foreach (var domainEvent in mvpEvent.DomainEvents)
                {
                    var revertOutboxMessage = OutboxMessage.Create(
                        domainEvent.GetType().AssemblyQualifiedName!,
                        JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                    await dbContext.AddOutboxMessageAsync(revertOutboxMessage, cancellationToken);
                }
            }

            var eventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.MvpSelected, 0, currentUserContext.UserId,
                request.TournamentTeamId, request.PlayerId);
            if (eventResult.IsFailure)
                return Result.Failure<SelectMatchMvpResponse>(eventResult.Error);

            await dbContext.AddMatchEventAsync(eventResult.Value, cancellationToken);

            var addedEvent = new MatchEventAdded(eventResult.Value.Id, match.Id, match.TournamentId, MatchEventType.MvpSelected.ToString());
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(addedEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(addedEvent, addedEvent.GetType())),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new SelectMatchMvpResponse(eventResult.Value.Id));
        }

        private Task<bool> IsOrganizerAsync(Guid tournamentId, CancellationToken cancellationToken) =>
            authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId, nameof(RoleScopeType.Tournament), tournamentId, MatchRoles.OrganizerOnly, cancellationToken);
    }
}
