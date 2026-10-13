using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.ClaimMatch
{
    internal sealed class ClaimMatchCommandHandler(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        ScheduleConflictChecker conflictChecker,
        ICurrentUserContext currentUserContext) : ICommandHandler<ClaimMatchCommand, ClaimMatchResponse>
    {
        public async Task<Result<ClaimMatchResponse>> Handle(ClaimMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<ClaimMatchResponse>(Error.NotFound("Match", request.MatchId));

            var refereeId = currentUserContext.UserId;

            var conflict = await conflictChecker.FindRefereeConflictAsync(match, refereeId, cancellationToken);
            if (conflict != null)
                return Result.Failure<ClaimMatchResponse>(conflict);

            var result = match.ClaimReferee(refereeId);
            if (result.IsFailure)
                return Result.Failure<ClaimMatchResponse>(result.Error);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            // Najpierw zapis meczu: gdy dwóch sędziów kliknie naraz, przegrany wypada tu na
            // DbUpdateConcurrencyException (Match.Version) i nie dostaje roli na meczu, którego nie prowadzi.
            await dbContext.SaveChangesAsync(cancellationToken);

            await userModule.AssignRoleAsync(
                refereeId,
                nameof(UserRole.Referee),
                nameof(RoleScopeType.Match),
                match.Id,
                cancellationToken);

            return Result.Success(new ClaimMatchResponse(match.Id));
        }
    }
}
